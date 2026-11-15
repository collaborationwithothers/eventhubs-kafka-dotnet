# Verifier mutation: acceptance trigger for any mutation input

This plan changes when a new verifier mutation run on `main` is required. Today only a change to a mutated file needs one. After this change, a change to any mutation input needs one, including tests and the shared code the targets call. Pre-merge sizing is parked by Hari's decision and is not part of this plan.

Revision: 3. Base main: `72a4a280ea755408c3d823cbbeed08d432a11e59`.
Issue: [474](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474).
Plan branch: `claude/474-plan`. Plan file: `items/474/plan-sizing-and-reuse.md`.
Plan name: `sizing-and-reuse`, kept from earlier revisions. It now covers one pull request, `mutation-acceptance-inputs`.

## Changes since revision 2

Revision 2 was checked in [plan check 6894284483](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894284483). [Decision 6894368044](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894368044) cuts the scope to revision 2's PR B and parks sizing. This revision changes scope, so it needs a new check.

- PR A (size split and hosted renewal) and everything only it needed is removed. That covers its schema, subcommands, test map, deployment assumptions D1 to D4 and open questions 1 to 5. The second check's design findings 1 to 4 and its `apply in revision` finding 6 concern only PR A, so they are void here. The measured evidence is recorded in the [parking comment 6894378761](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894378761).
- The mutation-input definition no longer defers to the check's output. It names the relevance test's conditions directly, and it settles the second check's P2 on "changed alone": the qualification file is a mutation input only together with another input (R2).
- The base moves to `72a4a280ea755408c3d823cbbeed08d432a11e59`. Between the old and new base only `README.md`, `docs/operations/evidence/issue-492-b10-verdict/make_results.py` and `results/B10/2026-11-15.md` changed. None is changed or cited here. `factory.md:198` is identical at both.

## 1. Goal and authority

Authority, in creation order:

1. The [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474), question 3 and its constraints: a passing run must cover every mutated file at the merged commit.
2. [Decision 6893688070](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6893688070): scope is questions 1 and 3.
3. [Decision 6894078013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894078013). Point 4: no `since` or `with-baseline`; the full fresh run on `main` stays. Point 5: a change to any mutation input needs a new passing run on `main`, worded as [check finding 9](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6893892580) requires. Point 6: Claude Code implements. Point 7: the ownership checks and their tests stay.
4. [Decision 6894120017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894120017): point 4's "Evidence pack is unchanged" refers to incremental runs only; point 5 changes the acceptance trigger.
5. [Decision 6894368044](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894368044): scope is cut to that trigger; sizing is parked.

This is a governance text change. It adds no hypothesis, expected outcome, verdict, code, test, workflow change or dispatch.

## 2. Evidence

- Today's trigger, `docs/agents/factory.md:198`: "Mutation acceptance for a pull request that changes any path in the `mutate` list of `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` is a passing `verifier-c5-mutation.yml` run dispatched on `main` after the change merges." Its later sentences say "the target-changing merge commit", "the target changes under review" and "modifies any configured target".
- The C5 configuration sets `"coverage-analysis": "off"` (`stryker-c5-config.json:43`). Stryker.NET 5.0.0 documents for `off`: "All unit tests are run against all mutants" ([configuration.md at 6e77a3451bac4793e9c839c3ff4055c1b30ca3af](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md), "coverage-analysis"; the tag `dotnet-stryker@5.0.0` peels to that commit, `git ls-remote`, 2026-11-15). Inference: a mutant's status depends on every test and on the code those tests run. So a change to a test, or to non-target code, can change a mutant's result. Today's trigger does not require a new run for such a change.
- The relevance test of the required qualification check, `c5_runtime.py:1131-1140` at the base, treats a change as relevant when any of these holds:
  - the shared input map from `input_maps` differs between base and head;
  - a configured target's source hash differs;
  - `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py` changed;
  - `.github/workflows/verifier-c5-mutation.yml` changed;
  - `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` changed;
  - the base input map cannot be computed (a target or supported dependency new in the head).

  The shared input map covers:
  - the C# and project inputs of `Parchwell.Verifier`, `Parchwell.Kafka`, `Parchwell.Scenarios` and the mutation-included verifier tests;
  - the root build files;
  - `.config/dotnet-tools.json`;
  - `docs/operations/a01-runtime-limits.json`;
  - `scenarios/profiles/*.yaml`;
  - `c5_projection.py` and `stryker-c5-config.json`.

  It is defined at `c5_runtime.py:429-468`.
- The last passing complete run, [43670258319](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43670258319), tested `1e01aea24a457fffe0dc970703af2de608d524e3`. `check --base 1e01aea24a457fffe0dc970703af2de608d524e3 --head 72a4a280ea755408c3d823cbbeed08d432a11e59` returned `{"reasons": [], "status": "not_applicable"}` (run in a scratch worktree, 2026-11-15). So no mutation input has changed since that run, and the new trigger leaves no merged change without an acceptance run.

## 3. Scope

One pull request, `mutation-acceptance-inputs`.

Allowed files:

- `docs/agents/factory.md`, "Evidence pack", the paragraph at line 198 only. A protected path; Hari merges.
- `docs/operations/verifier-mutation.md`, section "Runtime qualification diagnostics": one paragraph added after its second paragraph (the one that starts "The required `.NET verification` check validates").
- `items/474/state.md`, by the implementing session under docs/agents/factory.md "Item state file".

Size estimate, 2026-11-15: 10 to 25 changed lines in 3 files. Basis: one paragraph replaced, one added, and the state file updated. Not a limit.

Paths that must not change: everything else. In particular `tests/`, `src/`, `.github/workflows/`, `docs/operations/workflows/`, `results/`, any `expected.yaml`, `AGENTS.md`, `CLAUDE.md` and skills.

## 4. External contracts

The contract is the published gate wording in R1 and R2. No code, wire format, CLI or Kafka setting changes.

## 5. Requirements

R1. WHEN the pull request merges, THEN the paragraph at `docs/agents/factory.md:198` is replaced by exactly:

> Mutation acceptance for a pull request that changes any mutation input, as the [operational definition](../operations/verifier-mutation.md#runtime-qualification-diagnostics) defines it, is a passing `verifier-c5-mutation.yml` run dispatched on `main` after the change merges. The run's `summary.json` must identify the commit actually tested, report `measurement_complete: true` and `break_passed: true`, and name a commit on `main` whose history includes the merge commit of that change. A passing run on a later main commit is acceptable when it includes the change under review. If a later main change modifies any mutation input, acceptance for that change requires a later passing run whose measured commit includes it. A missing, failed or incomplete latest applicable run blocks evidence-pack publication; if the run fails or is incomplete, open a fix issue. Do not use the deleted local sample as a mutation check.

R2. WHEN the pull request merges, THEN "Runtime qualification diagnostics" in `docs/operations/verifier-mutation.md` contains, after its second paragraph, exactly:

> A mutation input is anything whose change makes the relevance test of `c5_runtime.py check` true when it compares the pull request base with its head. The configured target sources are mutation inputs. So are the shared inputs that `input_maps` lists, the mutation runner `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, including its target and cut tables, and `.github/workflows/verifier-c5-mutation.yml`, including its matrix. A target or supported dependency that is new in the head is also a mutation input. `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` is a mutation input only when another mutation input changes in the same pull request. [Evidence pack](../agents/factory.md#evidence-pack) states when a mutation input change needs a new mutation run on `main`.

R3. WHEN the pull request merges, THEN nothing else in either file changes, and the paragraph at `factory.md:200` (runtime qualification before merge) is unchanged.

R4. WHEN a later reader decides whether a merged change needs a mutation run, THEN R1 is the only rule they apply. R2 defines the term R1 uses. R1 and R2 do not restate each other: the rule is stated once, in the file that owns it, and the other file cites it.

## 6. Invariants and safety rules

- The mutation method, test population, assertions, thresholds, coverage mode, Stryker pin, layout and qualification check are unchanged.
- No incremental mode is introduced (decision 6894078013 point 4).
- No workflow dispatch, Azure credential or secret. Hard safety rules 3, 7, 8 and 9 hold.
- The change can only add required mutation runs. It removes no case where a run is required today: every `mutate` path is a configured target source, so it is a mutation input.

## 7. Test map

There is no code, so there are no unit tests. The checks are scripted text comparisons, run by the implementer and recorded in the PR notes.

| Check | Requirement | Input | Expected |
|---|---|---|---|
| T1 | R1 | `sed -n 198p docs/agents/factory.md` on the PR head | byte-equal to the R1 quote (without the `> ` prefix) |
| T2 | R2 | the added paragraph in `docs/operations/verifier-mutation.md` | byte-equal to the R2 quote, placed after the paragraph starting "The required `.NET verification` check validates" |
| T3 | R3 | `git diff --stat origin/main...HEAD` | exactly 3 files: the two above and `items/474/state.md` |
| T4 | R3 | `git diff origin/main...HEAD -- docs/agents/factory.md` | one paragraph removed and one added, both at line 198 |
| T5 | R2, R4 | the two links in R1 and R2 | both anchors exist (`#runtime-qualification-diagnostics`, `#evidence-pack`); `npm run check:docs` reports no broken link |
| T6 | section 6 | each path in the `mutate` list of `stryker-c5-config.json` | appears among `c5_workflow.TARGETS` paths, by script |

Standing fixture rule: the plan adds no check to code or tests, so no test fixture changes. If `npm run check:docs` rejects existing text in a changed file, the implementer handles it under docs/agents/pull-requests.md rule 8 and changes no assertion.

## 8. Deployment assumptions

None. The change is documentation; no test, deployed container, image, chart or runtime depends on it.

## 9. Acceptance commands

From the repository root, on the PR head:

```bash
npm run check:docs
git diff --stat origin/main...HEAD
python3 -c "import sys; sys.path.insert(0, 'tests/Parchwell.Verifier.Tests/Mutation'); import json, c5_workflow as w; m = json.load(open('tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json'))['stryker-config']['mutate']; t = {p for p, _ in w.TARGETS.values()}; print(all(p in t for p in m))"
```

Evidence:
- the docs check shows no new warning in changed files;
- the diff lists exactly the 3 allowed files;
- the script prints `True`;
- T1 and T2 pass by script.

The PR description records the T1 to T6 outcomes. Because `factory.md` is protected, Hari merges. The PR needs no mutation run: it changes no mutation input.

## 10. Open questions for Hari

None. Every choice is set by decisions 6894078013 point 5, 6894120017 and 6894368044. The wording follows check finding 9 and the second check's P2.

## 11. Sources

- `docs/agents/factory.md:198-200`, `docs/operations/verifier-mutation.md` lines 4 to 8, `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py:429-468` and `:1131-1140`, and `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json:43`, at the base commit.
- [Stryker.NET configuration.md at 6e77a3451bac4793e9c839c3ff4055c1b30ca3af](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md), "coverage-analysis". Read through a web-fetch subagent and checked by `grep` on the raw file, 2026-11-15.
- Actions REST API, run 43670258319, read 2026-11-15.

## 12. Plan-check answers

(a) Effort that scales with a number unknown at planning time: none. The change is fixed text in two files.

(b) Stricter than the parent plan: more changes need a mutation run on `main`, for example a test-only change. Hari adopted this in decision 6894078013 point 5. Nothing is looser.

(c) Dependencies on code, data or behaviour neither on `main` nor in the pull request: none. The definition in R2 refers to the relevance test on `main` (`c5_runtime.py:1131-1140`). If that test changes later, the definition follows it; that coupling is intended (R4).

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" in any of these cases:
- it needs to change R1's or R2's text, a requirement or the scope;
- `factory.md:198` on `main` no longer holds today's paragraph;
- `check --base 1e01aea24a457fffe0dc970703af2de608d524e3 --head origin/main` no longer returns `not_applicable` when the PR is opened. That would mean a merged change already needs an acceptance run, and Hari decides how to sequence it.
