# Plan: consumer verifier files as C5 mutation targets (#598)

Issue: [#598](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598). Plan name: `consumer-c5-targets`, an ordered set of three pull requests under docs/agents/factory.md "Item lifecycle" step 3 "Split by scope", plus one conditional pull request (section 2). This is the contract-grade plan for that set.

Revision 4. Base: `main` at `46a58ddd43d365486d5448f1cf5de35b696e33c3`. File paths and line numbers are on the base unless a line says otherwise. Paths under `src/Parchwell.Verifier/` are written relative to that folder where the C5 tables write them that way.

The consumer verifier code behind the published C03, C10 and C01 verdicts is not mutation-tested today. This plan adds three consumer verifier files as C5 mutation targets, measures their new shards on hosted runners before merge, and records their survivors after a passing paired run on `main`. The new shards need more mutation jobs than the 324 that the #494 capacity decision allowed; Hari allowed up to 40 more companion jobs in [decision 7185528103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185528103), which this revision applies.

## 1. Goal and authority

Goal: a passing paired C5 mutation run on one `main` commit whose population includes the consumer judgement, consumer phase evidence and consumer repeat extension files, within the 4,200-second paired limit, with every survivor in those files recorded as a finding.

Authorities on #598, in creation order (factory.md "Item lifecycle" step 3):

1. Issue body (updated 2026-11-26T06:37:08Z). Scope: decide which consumer verifier files become C5 targets, "At least the two above" (`VerifyRun/Consumer/Judgement/ConsumerJudgement.cs`, `Judge/ConsumerPhaseEvidence.cs`), checking "the other files under `VerifyRun/Consumer/` and `Judge/` that the consumer verdicts depend on"; size and qualify the shards before merge as #494 did; keep the paired acceptance within 4,200 s and state the added time as an estimate; "Any new jobs need a capacity decision from Hari." Constraints: `tests/Parchwell.Verifier.Tests/**`, `.github/workflows/` and `docs/` are protected; "Surviving mutants in the new targets are findings to record, not a reason to loosen a threshold or a test."
2. [Decision 7184606967](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7184606967): a Claude planning session writes this plan under `plan-pr` and gets a fresh check; open questions, "including any job-capacity or time-limit change, go to Hari"; no standing approval; protected-path merges stay with Hari.
3. [Decision 7184653835](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7184653835), which supersedes point 2's approval route: point 1, the `ready-for-agent` label approves the plan revision whose newest fresh check states "Blocking findings: none" and whose open questions read None, and the control session records that approval with the revision and commit; point 2, every open question goes to Hari before approval, including "any added mutation jobs beyond the 178 coordinator and 146 companion capacities, any change to the 4,200-second limit, and any threshold or test-set change"; point 3, implementing tool Claude Code; protected-path merges stay with Hari.

Decisions on #494 used as sources, not adopted as authority on #598: [7060787313](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7060787313) (at most 178 coordinator and 146 companion mutation jobs; diagnostic deadlines 2,650 s inner, 2,700 s outer, 50 minutes per job); [7145295306](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7145295306) (paired limit 4,200 s, measured "from the earlier run's creation ... through the last completion, including aggregation"; 2,700-second per-shard command ceiling and 50-minute hosted job limit unchanged). Evidence: the [#494 completion](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7155038729) (clean pair on `e3c8eb94`, `elapsed_seconds` 3046.0, raw score 9774/12715, `break_passed` true against break 60); [#474 control action 6925864409](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6925864409), the follow-up this issue replaces.

4. [Decision 7185528103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185528103), Hari's answer "All recommended" to [handoff 7185304016](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185304016) on revision 2: point 1, the three targets of 3.1, `Judge/JudgeHandler.cs` not added, the Common consumer codec and reader to a separate issue; point 2, up to 40 added companion jobs, companion capacity 146 + N with N fixed by hosted sizing, coordinator 178, the 4,200-second limit unchanged; point 3, deployment assumption D1 accepted by name; point 4, F4 option (a) with the handoff's acceptance line verbatim; point 5, the handoff's section 6 text verbatim. It authorizes this revision and one fresh check, and a clean result approves it under decision 7184653835.

5. [Decision 7185831322](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185831322), Hari's "yes, with 15 minutes" on [handoff 7185803183](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185803183): point 1, option (a), condition 8 replaced by the handoff's literal text, R12's citation to runbook lines 91 to 133, the `unaccounted_inputs` test row, and the third check's P2 marked `apply in implementation`; point 2 amends decision 7185528103 point 4 with a CI fallback: PR B may raise the `.NET verification` job's `timeout-minutes` from 10 to 15, and stops above 15 minutes; point 3, a hosted sizing measurement of a new shard above its ceiling, such as the `ReasonNames` declaration, stops and hands off, and nothing else is pre-approved. It authorizes this revision and one fresh check, and a clean result approves it under decision 7184653835.

No parent item, no split approved earlier, no plan comment on the issue before this one. Candidate: the whole issue.

## 2. Scope and pull requests

PR A and PR B touch protected paths (`.github/CODEOWNERS` line 7 `/.*/`, and `Parchwell.Verifier*/`). PR C touches only `docs/operations/`, which the ruleset excludes (`!/docs/operations/`); the issue body's constraint still sends it to Hari. No agent merges any of them.

**PR A, `consumer-c5-sizing-pin`** (branch `claude/598-consumer-c5-sizing-pin`). Files:
- `.github/workflows/verifier-c5-sizing.yml`: the `source_sha` default (line 10), the pinned source check (line 164) and the `matrix.include` rows (lines 24 to 137). Nothing else.
- `docs/operations/workflows/verifier-c5-sizing.yml.txt`: byte-identical to the workflow (AGENTS.md hard safety rule 8).
- `docs/operations/verifier-c5-sizing.md`: the opening section names the new pin, the new selection count and this issue; the previous pin moves to an "Earlier" section, as for the earlier repairs in that file.

**PR A2, `consumer-c5-sizing-pin-2`** (branch `claude/598-consumer-c5-sizing-pin-2`). Conditional: only when requirement R6 needs a second sizing pass. Same three files and rules as PR A.

**PR B, `consumer-c5-qualification`** (branch `claude/598-consumer-c5-qualification`). Files:
- `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`: `TARGETS` (line 27) and `PRIMARY_CUTS` (line 59) only.
- `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`: the `mutate` list (line 17) only.
- `.github/workflows/verifier-c5-mutation-companion.yml`: new `matrix.include` rows only.
- `.github/workflows/dotnet-verification.yml`: line 123 `timeout-minutes: 10` to `15`, only under section 8 command 6's fallback (decision 7185831322 point 2), and any committed copy of it.
- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py`: `CAMPAIGN_LIMITS` (line 327) and the additive-target bridge of section 3.4.
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py`, `test_c5_runtime.py`: the pins and tests of section 6.
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`.
- `docs/operations/verifier-mutation.md`: a new section "Issue 598 consumer targets" after "Issue 359 batching target", and the capacity sentence at line 63.

**PR C, `consumer-c5-survivor-record`** (branch `claude/598-consumer-c5-survivor-record`). File: `docs/operations/verifier-mutation.md`, the "Issue 598 consumer targets" section only.

Paths that must not change in any of these pull requests: every file under `src/`; every C# test file under `tests/`; `.github/workflows/verifier-c5-mutation.yml` (the coordinator); `tests/Parchwell.Verifier.Tests/Mutation/c5_campaign.py` (its raw hash is pinned as `CAMPAIGN_VALIDATOR_PIN` in `c5_runtime.py` line 341 and asserted in `test_c5_runtime.py`); `scenarios/`, `results/`, every `expected.yaml`; `tests/Parchwell.Verifier.Tests/stryker-config.json`; every workflow trigger.

Order: PR A, its dispatch, PR A2 and its dispatch only if needed, then PR B, the acceptance pair, then PR C. Each pull request is reviewable and passes its required checks on its own: PR A and PR A2 change no file that `c5_runtime.py check` treats as a mutation input (the sizing workflow is not in `input_maps`, `c5_runtime.py` lines 494 to 539), and PR C changes documentation only.

## 3. External contracts

### 3.1 Target set

The new C5 targets, in this order, appended after `verify-run-handler` in `TARGETS` and after `VerifyRun/VerifyRunHandler.cs` in the `mutate` list:

| Key | Path |
|---|---|
| `consumer-judgement` | `VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` |
| `consumer-phase-evidence` | `Judge/ConsumerPhaseEvidence.cs` |
| `consumer-repeat-extension` | `VerifyRun/Consumer/ConsumerRepeatExtension.cs` |

The set is Hari's decision ([decision 7185528103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185528103) point 1). The basis for each file, from a reader report checked against the cited lines:
- `ConsumerJudgement.cs` produces every consumer field the three verdicts read: the repeat status (line 79), `counts` (lines 91 to 102), `membership` for C03 (lines 137 to 168), `reassignment` for C01 (lines 169 to 176) and `subject_commit_coverage` for C10 (lines 181 to 191).
- `ConsumerPhaseEvidence.cs` decides which consumer block the judge accepts (`ConsumerEvidenceRules.Read`, lines 78 to 118) and builds the phase consumer status (`ConsumerPhaseJudge.Evaluate`, lines 258 to 285).
- `ConsumerRepeatExtension.cs` maps snapshot read errors to `consumer_start_assignment_invalid` (line 40) and `consumer_record_unavailable` (line 45), and gates the offset read on the deadline (lines 52 to 55).

Checked and not proposed:
- `Judge/JudgeHandler.cs`: about 15 of its 494 lines are consumer logic (lines 47 to 48, 258 to 268, 400 to 405, 426 to 427); the rest is producer and B10 judging. An estimated 600 to 800 mutants would add an estimated 12 or more jobs, mostly for non-consumer code.
- `VerifyRun/Consumer/Offsets/ConsumerCommittedOffsetReader.cs`: I/O around librdkafka. Its public constructor (lines 25 to 33) is reached only through dependency injection (`Program.cs` line 198), which no C5 test resolves, and `OffsetAuthenticationTests` uses the system clock (line 175) around a real librdkafka handle (lines 136 to 137); that this makes its timing load-sensitive is an inference.
- `VerifyRun/Consumer/IRepeatResultExtension.cs`, `VerifyRun/Consumer/Offsets/ConsumerOffsetRead.cs`, `Judge/PhaseCandidate.cs`: declarations only, no logic to mutate.
- `Common/Consumer/ConsumerArtifactCodec.cs` and `ConsumerArtifactReader.cs` decide consumer record validity, but they are outside the folders the issue names. Section 10a lists them as deferred.

### 3.2 Shards, cuts and placement

- Each new target with count above 1 has explicit `PRIMARY_CUTS`, so it gets a `-crossing` shard (`c5_workflow.py` `build_parts`, lines 268 to 280). A count-1 target has one shard named after its key.
- Cuts are UTF-16 offsets that never fall inside a `static readonly` declaration (`c5_workflow.py` lines 596 to 618). `ConsumerPhaseEvidence.cs` has such declarations at lines 29 to 36, 38 and 40 to 44, and `ConsumerJudgement.cs` at line 18. Each mutant inside them runs as its own isolated Stryker invocation, an estimated 127 s each (docs/operations/verifier-mutation.md, "Issue 340 static-field isolation", estimate dated 2026-11-03). The implementer places cuts so that these declarations sit in primaries whose isolated time fits requirement R4, using static-only primaries where needed, as `single-action-configuration-p2` and `-p15` already are (qualification JSON, `main_mutants` 0).
- `ConsumerPhaseEvidence.cs` uses `[GeneratedRegex]` (line 52), which no current target does. The hosted sizing (R5 to R7) shows how Stryker treats it; a sizing job that fails on it is handled under R6.
- `COHORTS` and every other runner byte outside `TARGETS` and `PRIMARY_CUTS` stay unchanged. Only those two literals are masked from the runner's execution hash (`c5_runtime.py` lines 372 to 383).
- All new shards go to the companion workflow, appended after `b10-final-registrations-crossing`, in `workflow.SHARDS` order. The coordinator matrix stays at exactly its 178 rows.
- `CAMPAIGN_LIMITS` becomes `{WORKFLOW: 178, COMPANION_WORKFLOW: 146 + N}`, where N is the number of new shards, with N at most 40 ([decision 7185528103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185528103) point 2). The companion therefore has at most 186 jobs, under GitHub's limit of "a maximum of 256 jobs per workflow run" (https://docs.github.com/en/actions/reference/limits, read 2026-11-26).

### 3.3 Sizing workflow (PR A and PR A2)

- Triggers stay `workflow_dispatch` only, and both main-only guards stay (lines 17 and 163). Timeouts stay: job 50 minutes, outer `2690s` with `--kill-after=10s`, inner `--timeout 2650`.
- `source_sha` default and the line-164 literal both become S, the full SHA of the measurement source commit of requirement R2.
- The matrix rows are exactly the new shards, each `shard`, `target` and `target_sha256`, where `target_sha256` is the SHA-256 of the target's bytes at S. In PR A2 the rows are exactly the selections that R6 names.

### 3.4 Additive-target bridge (`c5_runtime.py check`)

A new pinned route in `c5_runtime.py`. Two constants pin it: `CONSUMER_TARGETS_PRIOR`, the full SHA of the `main` commit M that S is built on, and the three keys of 3.1. The bridge is active only when the head's `TARGETS` contains all three keys. A head with some but not all of them gets today's routes unchanged. For each shard whose target is not one of the new targets, the bridge accepts the shard's existing proof only when all of these hold. A failure of conditions 1 to 7 adds `missing_measurement`. A failure of condition 8 adds `unaccounted_inputs`, through the existing assessment check (`provenance_errors`, `c5_runtime.py` lines 1777 to 1795):

1. The shard's proof is valid under the routes `validate` applies with M as the head and the qualification JSON committed at M as the ledger. At M, `check --head M` returns `qualified` (measured, 3.4 evidence below).
2. The shard's record under `targets`, and its `timing_evidence` entry, equal those at M after canonical JSON encoding, and so does the record's `source_sha256` and `evidence`.
3. The head's `TARGETS` equals M's `TARGETS` followed by exactly the 3.1 keys, in that order. The head's `PRIMARY_CUTS` equals M's plus entries for new keys only.
4. The head's `stryker-c5-config.json` equals M's with exactly the 3.1 paths appended to `mutate`.
5. The coordinator matrix equals M's. The companion matrix equals M's rows followed by exactly the new shards in `workflow.SHARDS` order.
6. `CAMPAIGN_LIMITS` is 178 for the coordinator and 146 plus the number of new shards for the companion.
7. Every byte of the runner (`c5_workflow.py`) outside `TARGETS` and `PRIMARY_CUTS`, of the campaign validator (`c5_campaign.py`), of the coordinator workflow, and of the companion workflow outside its `matrix.include` rows equals M's. The shard's generated `shard_config` equals M's.
8. Each retained timing context's assessments differ from M's only by: (i) for each 3.1 path, `shared_inputs['src/Parchwell.Verifier/<path>'] = {basis, current_sha256: null}` where the context's measured `shared_inputs` holds the path, and removal of that path's existing `shared_inputs` assessment where it does not; (ii) `target_sources['<path>'] = {basis, current_sha256: <head hash>}` for each 3.1 path; (iii) `shared_inputs['tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json'] = {basis, current_sha256: <head hash>}`, which the bridge accepts for this nonrenewable path only when condition 4 holds; (iv) ordinary version 2 assessments of renewable inputs that changed after M. Each `basis` is a nonempty list of strings citing this plan and decision 7185528103.

The new shards qualify through the existing measured route, with timing contexts whose `source_commit` is S (docs/operations/verifier-mutation.md, "Committed-tree configuration equivalence"). The bridge adds no reason name and changes no existing route. It computes the proofs at M once per checker process and reuses them for every retained shard (decision 7185528103 point 4).

Evidence that the bridge is needed, measured in this planning session on 2026-11-26. On a local scratch commit of M (`7132ff1254f08a68d8d12bd94f893a255e2bd0c7`, never pushed), two of the 3.1 targets were added with count 1: `TARGETS`, `mutate`, two companion rows and companion limit 148. The qualification was M's, plus `shared_inputs` recomputed at the scratch head, plus the moved-file assessments of item 8 in every context. The unchanged `validate`, wrapped by a script that recorded the shard for each reason it added, rejected 137 existing shards with `missing_measurement` (B10 final judge, projection codec, repeat evidence, intake, restart manifest, sweep codec, phase candidate codec, single-action configuration, verifier result codec and verify-run handler shards), 7 with `invalid_estimate` (`b10-final-judge-p9` and `-p10`, `b10-final-projection-codec-p11` and `-p12`, `b10-final-repeat-evidence-p7` and `-p8`, `phase-p6`) and 1 with `layout_mismatch` (`verify-run-handler-p7`), each added inside `validate` itself; the script did not trace which pinned route each shard was on, and the ledger as a whole with `invalid_renewal` and `unaccounted_inputs`. A second script found that every one of the 324 existing shards' generated `shard_config` is identical on M and on the scratch commit. So the additive change leaves existing selections unchanged and breaks only the pinned proof routes, which compare the head with pinned trees (`same_timing_layout`, `c5_runtime.py` lines 1074 to 1080). Baseline: `check --head M` on a full-history clone returned `qualified` in 48 s. On the shallow session clone it returned `invalid_input`.

### 3.5 Unchanged values

The per-shard `budget_seconds` stays 2700. `BUDGET = 4200` (`c5_campaign.py` line 18) stays. Break 60, coverage analysis `off`, the mutation-only C# test population (`ParchwellVerifierMutationOnly=true`, `c5_workflow.py` line 330) and every existing shard and target stay. The plan changes no threshold and no test set (decision 7184653835 point 2).

### 3.6 Runbook text (PR B)

The "Issue 598 consumer targets" section states: the 3.1 targets and their shard names; N; S and the sizing run IDs; the bridge's conditions in one paragraph that cites this plan; and the capacity decision 7185528103 point 2. Line 63's capacity sentence changes to the new companion limit, citing that decision. PR C appends the survivor table of R11.

## 4. Requirements

R1. WHEN the implementer prepares cuts, THEN it may run, in its own Linux workspace on S-candidate commits, at most 6 `c5_runtime.py local-shard` invocations of new shards, each with `--timeout` at most 2700. Exhaustion action: push -wip and hand off to the next session. The local receipts are diagnostic only and are not qualification evidence.

R2. WHEN the cuts are chosen, THEN the implementer commits S on M: `TARGETS`, `PRIMARY_CUTS`, `mutate`, the companion rows and `CAMPAIGN_LIMITS` exactly as in 3.1 and 3.2, with N at most 40. It pushes S to `claude/598-consumer-c5-qualification-wip` and verifies S through the commit API. WHEN N would exceed 40, THEN it stops and hands off to Hari with the measured mutant counts.

R3. WHEN S exists, THEN a script confirms that each existing shard's `shard_config` at S equals its config at M. WHEN any differs, THEN the implementer stops and hands off.

R4. WHEN PR A is prepared, THEN each new shard's projected estimate is at most 2,150 s. The projection uses the shard's mutant count from R1 or from source structure, 34.92 s of wall time per main mutant (the median of `(estimated_seconds - setup_seconds - isolated_seconds) / main_mutants` over the 301 qualified shards with `main_mutants > 0`), 183 s setup (their median `setup_seconds`) and 127 s per isolated mutant. PR A's description records each projection, labelled an estimate.

R5. WHEN Hari merges PR A, THEN the implementer dispatches `verifier-c5-sizing.yml` once on `main` with `source_sha` S, and downloads every artifact before its one-day expiry. Cap: one dispatch.

R6. WHEN a new shard's sizing job times out, has no receipt, or yields an estimate above 2,150 s, THEN the implementer moves cuts for that target. It re-measures every selection whose generated configuration changed, including that target's crossing selection, through PR A2 and one further dispatch on the new source S2. S2 replaces S everywhere below. A lost receipt with an unchanged configuration is re-measured in PR A2 unchanged. Cap: one PR A2 and one dispatch. Exhaustion action: push -wip and hand off to the next session, with every receipt and failure preserved. WHEN the shard above the ceiling is one whose time comes from a `static readonly` declaration that no cut can split, such as `ReasonNames` (`ConsumerPhaseEvidence.cs` lines 29 to 36), THEN moving cuts cannot help, and the implementer stops and hands off to Hari (decision 7185831322 point 3).

R7. WHEN every new shard has a complete hosted receipt, THEN the implementer records each receipt under the rules of the #494 timing-margin repair: artifact ZIP size and SHA-256, commit, tree, target, selector, complete native identity set and workers. It requires 0 < observed_seconds <= 2650, workers 1 or 2, `estimated_seconds` >= observed_seconds, and `estimated_seconds` <= 2150. `estimated_seconds` = `ceil(setup_seconds + main_work_seconds / parallelism + isolated_seconds)` (`c5_runtime.py` line 2438), and each basis states `observed_workers=N`.

R8. WHEN PR B is opened, THEN its branch has S as an ancestor, and its description asks Hari to merge it with a merge commit, so S stays an ancestor of `main` (the checker reads target bytes at each context's `source_commit`, `c5_runtime.py` line 847), and `c5_runtime.py check --base <PR base> --head <PR head>` returns `qualified` in the required `.NET verification` check. The new targets' records use hosted evidence of kind `hosted_complete` bound to S, and each new shard has a `measured` `timing_evidence` entry.

R9. WHEN `check` validates a retained shard on a head whose `TARGETS` holds all three 3.1 keys, THEN the bridge of 3.4 applies. A failed condition 1 to 7 adds `missing_measurement`, and a failed condition 8 adds `unaccounted_inputs`. WHEN the head's `TARGETS` lacks any of the 3.1 keys, THEN the bridge does not apply, and the result equals today's result for that head.

R10. WHEN Hari merges PR B, THEN the implementer runs one acceptance pair on the merge commit, under docs/operations/verifier-mutation.md "Runtime qualification diagnostics" steps 1 to 4, including its bounded recovery (at most four re-runs per run). It runs `c5_campaign.py terminal` on a full-history checkout. Acceptance: both runs succeed with every job; `measurement_complete` true and `incomplete_reasons` empty; `break_passed` true; terminal `status` `met` within 4,200 s; `campaign.json` lists 324 + N physical shards. WHEN the terminal check reports `missed`, or recovery is exhausted, THEN the implementer hands off to Hari with the evidence. It changes no limit and no threshold.

R11. WHEN the acceptance pair passes, THEN PR C records, by script from that run's reports, each new target's killed, survived, no-coverage, timeout and compile-error counts and its score, and one row per mutant whose status is not Killed, giving id, line and column span, mutator and status. A non-Killed mutant on a line that section 3.1 names as producing a C03, C10 or C01 verdict field is listed in PR C's description as a finding for Hari. The rows cover every status in `c5_workflow.py` `STATUSES` (line 321) except `Killed` and `Ignored`; `Ignored` is counted, not listed. The verdict-field lines are: `ConsumerJudgement.cs` lines 24 to 105 and 123 to 191; `ConsumerPhaseEvidence.cs` lines 78 to 214 and 252 to 285; `ConsumerRepeatExtension.cs` lines 36 to 55. Line numbers are those at the acceptance commit; if a target's source changed after the base, PR C maps the ranges by member name. No test, threshold or verdict changes.

R12. WHEN `main` changes a shared input after M and before PR B merges, THEN PR B assesses each change under the version 2 rules (docs/operations/verifier-mutation.md lines 91 to 133). Cap: 20 changed paths. Exhaustion action: push -wip and hand off to the next session. WHEN such a change touches a 3.1 target's source, THEN that target's receipts are stale. The implementer stops and hands off.

## 5. Invariants and safety rules

- No `src/` file changes, so no published verdict's code changes.
- Every existing shard keeps its record, estimate, timing evidence and selection byte for byte (3.4 item 2, R3).
- Mutation workflow triggers are unchanged. Every dispatch is on `main` (AGENTS.md hard safety rule 8). No workflow reaches Azure, and none gains a pull request trigger (rule 9).
- The coordinator matrix is unchanged. The companion grows by exactly N rows.
- PR B merges with a merge commit, so S and S2 stay ancestors of `main` (R8).
- Survivors are recorded, not fixed by loosening (issue body; GOVERNANCE "Verdict integrity").
- Numbers in pull requests and the runbook are measured or labelled estimates (AGENTS.md hard safety rule 11).

## 6. Test map

All tests are Python unit tests under `tests/Parchwell.Verifier.Tests/Mutation/`, run by `.NET verification` (`dotnet-verification.yml` lines 139 to 152).

| Req | Test (file) | Input | Expected |
|---|---|---|---|
| 3.1, 3.2 | `test_literal_targets_parts_and_shards_match_code_config_and_workflow` (test_c5_workflow.py) | committed tree | literal `targets` dict has 34 entries, the last three as in 3.1; matrix lengths `[178, 146 + N]` (line 1128) |
| 3.2 | same file, `EXPECTED_JOB_NAMES` and counts (lines 273, 3292, 3294) | committed tree | `324 + N` job names; `34` mutate paths; `EXPECTED_PRIMARY_CUTS` (line 64) gains exactly the new keys |
| 3.2 | same file, `EXPECTED_COMPANION_TARGETS` (line 47) | committed tree | 17 paths: the 14 of the base plus the three of 3.1 |
| R9 | new `test_consumer_targets_bridge_accepts_committed_layout` (test_c5_runtime.py) | the PR head | `check` returns `qualified` |
| R9, 3.4 item 1 | new `test_consumer_bridge_rejects_invalid_prior_proof` | `CONSUMER_TARGETS_PRIOR` patched to a synthetic commit equal to M except that one retained shard's basis reads `observed_workers=1` while its `parallelism` is 2, with the head ledger edited the same way | `rejected`, reasons contain `missing_measurement` |
| R9, 3.4 item 2 | new `test_consumer_bridge_rejects_changed_retained_record` | head JSON with one retained shard's `basis` text extended by ` Edited.`, keeping `observed_workers=` and every number | `rejected`, reasons contain `missing_measurement` |
| R9, 3.4 item 3 | new `test_consumer_bridge_rejects_reordered_targets` | head `TARGETS`, `mutate` and companion rows with the 3.1 keys in reverse order, all consistent with each other | `rejected`, reasons contain `missing_measurement` |
| R9, 3.4 item 4 | new `test_consumer_bridge_rejects_changed_config` | head `stryker-c5-config.json` with `coverage-analysis` `perTest`, `mutate` as 3.1 requires | `rejected`, reasons contain `missing_measurement` |
| R9, 3.4 item 5 | new `test_consumer_bridge_rejects_moved_coordinator_row` | one existing coordinator row exchanged with one existing companion row, both of retained shards, limits 178 and 146 + N, so both matrices keep their sizes | `rejected`, reasons contain `missing_measurement` |
| R9, 3.4 item 6 | new `test_consumer_bridge_rejects_wrong_companion_limit` | companion limit 147 + N, matrix unchanged | `rejected`, reasons contain `missing_measurement` |
| R9, 3.4 item 7 | new `test_consumer_bridge_rejects_changed_validator` | one comment line appended to `c5_campaign.py` | `rejected`, reasons contain `missing_measurement` |
| R9, 3.4 item 8 | new `test_consumer_bridge_rejects_missing_move_assessment` | one context without the `null` assessment for `ConsumerJudgement.cs` | `rejected`, reasons contain `unaccounted_inputs` |
| R9, 3.4 item 8 (iii) | new `test_consumer_bridge_rejects_missing_config_assessment` | one context without the `stryker-c5-config.json` assessment at the head hash | `rejected`, reasons contain `unaccounted_inputs` |
| R9 second clause | new `test_consumer_bridge_inactive_without_all_keys` | head with only `consumer-judgement` added, its record and shards measured | `rejected`, reasons contain `missing_measurement` |
| R9 second clause | new `test_consumer_bridge_inactive_at_prior` | head M | `qualified` |
| R9 | existing `test_new_target_has_no_default_pass` (line 2023) and `test_additive_target_preserves_committed_invocation` (line 436) | unchanged | unchanged expectations |
| R7 | new `test_consumer_new_shards_within_ceiling` | committed JSON | each new shard has `estimated_seconds` <= 2150 and a `measured` timing entry whose `source_commit` is S |

Boundaries: R7's ceiling is tested at the committed values. Each bridge condition has one rejection test that changes only that condition's input, plus the accepting committed head. Two cases cannot be fully isolated, and the tests assert only the bridge's own reason. Condition 1 is the existing proof routes evaluated at M, so any input that makes M's proof invalid also breaks the same shard under today's routes; those routes keep their own tests. Condition 4's changed config is also a nonrenewable input, so other reasons may be added as well. The inactive cases are a head without the keys (the prior) and a head with some of them.

Standing fixture rule. When an existing test's fixture is rejected by a new check, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Basis: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. The literal layout pins in the first three rows, and the literal `shards` dict in the same test (`test_c5_workflow.py` lines 793 to 1116), are layout data, not assertions loosened: they change to the new committed layout.

When an existing test in `test_c5_runtime.py` validates a historical head against the current runner tables, the implementer runs it under the existing `historical_layout` helper (lines 298 to 309), or an equivalent that loads the runner committed at that test's pinned head, and keeps every assertion and input. Cap: 15 tests. Exhaustion action: push -wip and hand off to the next session. The PR implementation notes list each such test. (`ObservedTailLedgerTests`, lines 4122 to 4242, is the known case; decision 7185528103 point 5.)

## 7. Deployment assumptions

D1. GitHub runs 324 + N standard hosted jobs of the pair at once, without material queueing. Evidence: the jobs API records of the last clean pair (companion run 45555978345 and coordinator run 45555996117, attempt 1, completed 2026-11-25), read after completion, not a live probe, under full campaign load. All 324 mutation jobs were running at the same time, and the largest job queue time was 6 s (median 4 s), computed by script on 2026-11-26. No evidence exists above 324 concurrent jobs, and the repository records no plan or concurrency limit. GitHub lists 500 concurrent standard jobs for Enterprise and 60 for Team (https://docs.github.com/en/actions/reference/limits, read 2026-11-26). No offline test covers this. Hari accepted this risk by name in [decision 7185528103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185528103) point 3: if it fails, the post-merge terminal check reports the overrun, and the implementer hands off without changing the limit.

D2. Hosted runner speed. Covered pre-merge by the hosted sizing measurement (R5 to R7) and post-merge by R10. The 2,150-second ceiling keeps a margin for the 23.2% worker-time increase seen on slower hosts ([comment 7060563104](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/569#issuecomment-7060563104)).

## 8. Acceptance commands

1. PR A and PR A2: `cmp .github/workflows/verifier-c5-sizing.yml docs/operations/workflows/verifier-c5-sizing.yml.txt` exits 0. `git diff --name-only origin/main...HEAD` lists exactly the three PR A files.
2. PR B, from the repository root on a full-history clone: `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_*.py' -v` passes, and `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <base> --head <head> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` prints `"status": "qualified"`.
3. PR B: `dotnet build Parchwell.slnx -warnaserror` and `npm run check:docs` pass, under docs/agents/pull-requests.md "Verification gates".
4. PR B: the R3 script prints zero changed existing configurations. PR B's implementation notes record its output.
5. After PR B merges: the R10 pair and `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_campaign.py terminal ...` with `status` `met`, recorded in a comment on #598.
6. PR B: the `.NET verification` job on PR B's final head completes within its 10-minute timeout; PR B's description records the job's duration and the qualification step's duration. If it does not, the implementer stops and hands off; it does not change the workflow.
   Amended by decision 7185831322 point 2: if the job exceeds its 10-minute timeout on PR B's final head, PR B raises that job's `timeout-minutes` from 10 to 15 in `.github/workflows/dotnet-verification.yml` (line 123), and nothing else in that file. No committed copy of that workflow exists under `docs/operations/workflows/` on the base (listed 2026-11-26); if one exists when PR B is built, it gets the same change, byte for byte (AGENTS.md hard safety rule 8). No trigger changes. PR B's description records both durations at 10 and at 15 minutes. If the job exceeds 15 minutes, the implementer stops and hands off. Hari merges the protected change.
7. PR C: `npm run check:docs` passes, and the R11 table is produced by a script whose command PR C's description records.

## 9. Sources

- docs/operations/verifier-mutation.md at the base: "Runtime qualification diagnostics" (lines 4 to 108), "Committed-tree configuration equivalence" (109 to 132), "Issue 340 static-field isolation" (300 to 329), "Issue 359 batching target" (7749 on).
- docs/operations/verifier-c5-sizing.md at the base.
- `c5_workflow.py`, `c5_runtime.py`, `c5_campaign.py`, `test_c5_workflow.py`, `test_c5_runtime.py`, `stryker-c5-config.json`, the qualification JSON and the three C5 workflows at the base, at the lines cited above.
- Source files of section 3.1 at the base, at the lines cited.
- GitHub Actions limits, https://docs.github.com/en/actions/reference/limits, read 2026-11-26 through a web-fetch subagent: "A job matrix can generate a maximum of 256 jobs per workflow run"; standard hosted runner concurrency 20 (Free), 40 (Pro), 60 (Team), 500 (Enterprise). The page says "These limits are subject to change."
- Issue comments cited in section 1, and [comment 7060563104](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/569#issuecomment-7060563104).
- Jobs API of runs 45555978345 and 45555996117, attempt 1 (section 7 D1).

External behaviour (plan rule 1): the plan relies on no Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour. The Stryker behaviour it relies on (static field isolation, one mutant per isolated run) is the repository's cited record in "Issue 340 static-field isolation", pinned to Stryker 5.0.0. The tools used were the GitHub REST API, git and a web-fetch subagent for the GitHub page.

Estimates (AGENTS.md hard safety rule 11, all dated 2026-11-26):
- Mutants: `ConsumerJudgement.cs` 500 to 700, `ConsumerPhaseEvidence.cs` 350 to 500, `ConsumerRepeatExtension.cs` 40 to 60. Basis: a reader subagent's regex count of mutable operators, literals and statements; no Stryker run.
- New shards N: 21 to 33, from those counts at 34.92 to about 39.8 s per main mutant (median and 75th percentile, 39.81 or 39.82 s by percentile method, over the 301 qualified shards), 183 s setup, a 2,150-second projection ceiling, one crossing shard per split target and 3 static-only shards for `ConsumerPhaseEvidence.cs`. Computed by script.
- Added paired time: 13 to 66 s, if D1 holds and no new shard runs longer than the existing tail. Basis: companion downloads took 93 to 240 s for 146 shards ([decision 7145295306](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7145295306)), scaled to 20 to 40 more shards. The last clean pair's 3,046 s would become about 3,112 s, under 4,200 s. The paired time is bounded by the longest job (2,794 s in the last pair, `single-action-repeat-p18`) plus aggregation, and new shards projected at most 2,150 s do not extend it. If D1 fails, queueing adds time that this estimate does not cover.
- Score: even with no new mutant killed, 9,774 of 12,715 + 1,300 is 69.7%, above break 60.

## 10. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Work that scales with a runtime number: local cut-preparation runs (R1, cap 6 invocations, each at most 2,700 s); hosted sizing (R5 and R6, cap two dispatches and one PR A2); new shard count N (cap 40 by decision 7185528103 point 2, stop at R2); acceptance recovery (R10, the runbook's at most four re-runs per run); survivor rows (R11, produced by script, no per-survivor work); shared-input assessments after M (R12, cap 20 paths); historical-layout test fixtures (section 6, cap 15 tests); the CI job duration (section 8 command 6: raise to 15 minutes if 10 is exceeded, stop and hand off beyond 15); PR B cold review (docs/agents/pull-requests.md rule 8 budget).

(b) Stricter than its sources: the 2,150-second estimate ceiling for new shards, against the 2,700-second budget and 2,650-second inner deadline. Why: #494's slow-host study chose 2,150.5 s for a 499.5 s margin (comment 7060563104), and a new shard above the current tail would extend the paired time. The bridge requires byte-identical retained records, stricter than the generic committed-tree route, because the measured evidence above shows that route does not cover an added target.

(c) Dependencies outside `main` and the pull requests: the hosted sizing artifacts (R5 to R7), which the checker binds by digest but does not download (docs/operations/verifier-mutation.md line 119). Reviewers inspect them before expiry. D1 is the one deployment assumption no test covers.

## 10a. Deferred work

- Common consumer codec and reader as targets (`Common/Consumer/ConsumerArtifactCodec.cs`, `ConsumerArtifactReader.cs`). Evidence: they decide record validity (reader report, codec lines 45 to 60, reader lines 21 to 58). Not on the path of this issue's acceptance. Deferral is permitted because the issue names the `VerifyRun/Consumer/` and `Judge/` folders. Hari decided it becomes a separate issue (decision 7185528103 point 1). Responsible: the #499 control session or a session Hari directs; no pull request of this plan creates it. Gate: none in this plan.
- Survivor dispositions and test additions for the new targets. Optional; R11 records the survivors as findings. Responsible: a later issue if Hari opens one. Gate: none.
- Acceptance pair (R10). Authorized only after PR B merges. Responsible: the implementing session. Cap and exhaustion: R10. Completion evidence: the terminal report. Gate: PR C.

## 11. Open questions for Hari

None.

## 11a. Answered questions

All answered by [decision 7185528103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185528103):
- Q1, target set: the three files of 3.1; `Judge/JudgeHandler.cs` not added; the Common consumer codec and reader become a separate issue (10a).
- Q2, capacity: up to 40 added companion jobs; companion limit 146 + N with N fixed by hosted sizing; coordinator 178; the 4,200-second limit unchanged (3.2, R2).
- Q3: deployment assumption D1 accepted by name (section 7).
- F4 (second check): option (a), prior proofs computed once per process (3.4) and acceptance command 6 (section 8).
- F5 (second check): the historical-layout fixture rule in section 6, cap 15.
- Third check's finding on condition 8: [decision 7185831322](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185831322) point 1, option (a), applied in 3.4.
- CI headroom: [decision 7185831322](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185831322) point 2, the 15-minute fallback in section 8 command 6.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope; when N would exceed 40; when an existing shard's configuration or record would change; when a cap in section 10 (a) is exhausted; or when R10 fails.

## 13. Size estimate

Estimates under AGENTS.md hard safety rule 11, dated 2026-11-26, from the base file sizes and the N estimate. Size is reported, not enforced.
- PR A: 3 files, about 370 to 450 changed lines (38 rows of 3 lines out and 21 to 33 rows in, in two byte-identical files, 364 to 436 lines by script, plus the two pin lines and about 10 runbook lines).
- PR A2, if needed: 3 files, under 150 lines.
- PR B: 8 files, or 9 with the CI fallback, about 1,500 to 2,000 changed lines, most of them qualification JSON records.
- PR C: 1 file, about 50 to 300 lines, depending on the survivor count.

## 14. Changes since revision 3

Applies [decision 7185831322](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185831322) and the P2 of the [third plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185786266):
- Section 1 adds the decision as authority 5.
- 3.4 condition 8 is the handoff's literal text, including the `stryker-c5-config.json` assessment and removal where a context never measured a moved file.
- R12 cites runbook lines 91 to 133.
- The test map adds the missing-config-assessment row, and the condition 5 row exchanges rows at unchanged limits (the P2 marked `apply in implementation`).
- Section 8 command 6 adds the 15-minute fallback. Section 2 lists `dotnet-verification.yml` for that fallback only.
- R6 states point 3's stop for a shard that is over the ceiling because of a static declaration no cut can split.

## 14b. Changes since revision 2

Applies [decision 7185528103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185528103) and the non-blocking notes of the [second plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185285667):
- Section 1 adds the decision as authority 4. The opening paragraph, 3.1, 3.2, R2 and the stop rule now state the decided target set and the cap of 40.
- Section 7: D1 is accepted by name, with a link.
- 3.4: the prior proofs are computed once per process. Section 8 adds the handoff's acceptance line verbatim as command 6 (F4).
- Section 6 adds the handoff's historical-layout text verbatim (F5) and names the literal `shards` dict pin.
- Section 10a: the Common codec and reader become a separate issue.
- Section 11 reads None; 11a records the answers. Section 10 (a) adds the new caps.
- Notes: the scratch evidence names the 8 shards of the other two reasons, 3.2 notes `[GeneratedRegex]`, and section 9 gives the 75th percentile as about 39.8 s.

## 14a. Changes since revision 1

Applies the [first plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7185072986):
- Finding 4: 3.4 and R9 now give `unaccounted_inputs` for condition 8 and `missing_measurement` for conditions 1 to 7. The test row matches.
- Finding 5: the bridge is active only with all three keys (3.4, R9). New rejection tests cover conditions 1, 4 and 7, plus a partial-key head and the prior head. The condition 2 input now changes only `basis` text. The boundaries paragraph explains the two cases that cannot be fully isolated.
- Finding 6: R8 and section 5 require PR B to merge with a merge commit.
- P2-1, P2-2, P2-3, P2-4, P2-5 and P2-6: the protection sentence, the location of `CAMPAIGN_VALIDATOR_PIN`, the PR A size, N recomputed by script as 21 to 33, R11's line ranges and statuses, and the librdkafka note marked as inference.
- The check's answer (a): R12 now has a cap of 20 paths.

Findings 1 to 3 are open questions for Hari and stay in section 11.
