# repo-review-scope contract-grade plan (issue #414)

Revision: r2

This plan replaces the single "anything" row of the cold-review rubric in `.agents/skills/repo-review/SKILL.md` with rows keyed to what a diff touches, as issue #414 proposes and Hari's decision of 2026-11-09 accepts. The rubric text below follows the accepted split. Three points that the issue and the decision leave open are listed under "Open questions for Hari"; the plan decides none of them, and it is not approvable until they are answered.

Revision r1 (commit 2a7797f1e287d2afdc59c4571871113efa19ef18) was pushed but never posted. It departed from the proposal in three places. Decision 6744694270, created one second before that push, accepts the proposal as written, so r2 follows the proposal and r1 is withdrawn.

## Goal and authority

Goal: scope the rubric's reading load to the documents a diff's files depend on (issue #414, "Proposal").

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3):
1. The body of issue #414, `updated_at` 2026-11-09T14:01:42Z: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414
2. `## Decision: answers to the two open questions`, comment 6744694270, created 2026-11-09T16:00:54Z: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414#issuecomment-6744694270. Point 1: "The proposed rubric split is accepted." Point 2: "Measure it on the next three pull requests that get a cold review: run time per round and any P0 or P1 found later by Codex or Hari that the cold review missed. Hari then decides whether it stays."

There is no approved item plan and no parent issue. This is directly authorized governance work: the implementer records the task brief, the issue, the decision and the approval of this plan in the receipt (docs/agents/pull-requests.md, execution-card paragraph), and claims no item plan.

## Scope

Files allowed to change:
- `.agents/skills/repo-review/SKILL.md`, only between the line `## Rubric` and the line `## Report`.
- `items/414/state.md`, written under docs/agents/factory.md "Item state file".
- One authored receipt under `items/414/`, with a file name the implementer chooses.
- Any file that the answers to open questions 1 and 2 add. Until then, none.

Paths that must not change: every other path, including AGENTS.md, CLAUDE.md, docs/agents/factory.md, docs/agents/pull-requests.md, `.claude/agents/repo-reviewer.md`, the symlink `.claude/skills/repo-review`, and the frontmatter and every section of `SKILL.md` other than "Rubric".

`.agents/skills/repo-review/SKILL.md` is a protected path (`.github/CODEOWNERS`, line `/.*/`), so Hari merges (AGENTS.md GOVERNANCE "Merge rule"; AGENTS.md "Skills").

## External contracts

The rubric is published wording that every later cold review reads. After the change, the text from the line after `## Rubric` up to the line before `## Report` is exactly the block below, byte for byte, with one blank line before and after the table.

```text
| The diff touches | Apply |
|---|---|
| anything | AGENTS.md: all of GOVERNANCE (scope and deferred items included), "Code Review Rules", writing standards; the originating specification identified in the receipt |
| `AGENTS.md`, `CLAUDE.md`, `docs/agents/`, `.agents/skills/`, `.claude/`, `.codex/`, `items/` | docs/agents/factory.md and docs/agents/pull-requests.md, in full |
| `docs/engineering/` | each changed document itself |
| .NET code or tests | docs/engineering/dotnet.md; docs/blueprint.md ADRs |
| Terraform | docs/engineering/terraform.md; ADR-011; docs/blueprint.md ADRs |
| Kafka client settings | docs/blueprint.md "Workloads (.NET 10, Confluent.Kafka 2.16.0 pinned)": every setting traceable to an inventory item; docs/blueprint.md ADRs |
| `scenarios/`, `results/` | AGENTS.md verdict integrity; docs/blueprint.md ADRs |
| `.github/workflows/` | AGENTS.md safety rules 8 and 9; docs/blueprint.md ADRs |
| a file that a rule elsewhere in the repository cites by name | the citing rule |
```

Mapping to the proposal's four bullets:
- "Always" is the "anything" row.
- "Process files" is the second row, with the proposal's seven paths and its two documents.
- "Code, Terraform, scenarios, workflows: the blueprint ADRs and the engineering documents those rows already name" adds `docs/blueprint.md ADRs` to the five existing code rows and keeps what each already names. The `docs/engineering/` row is not code and is unchanged.
- "Any changed file that a rule elsewhere cites by name: the citing rule" is the last row.

The rest of the skill, including step 2's rule that a changed rule document is applied at its `origin/main` version, is unchanged.

## Requirements

R1. WHEN a cold reviewer applies the changed skill to any file in scope, THEN it applies the "anything" row.

R2. WHEN the diff touches `AGENTS.md`, `CLAUDE.md`, or a path under `docs/agents/`, `.agents/skills/`, `.claude/`, `.codex/` or `items/`, THEN the reviewer applies docs/agents/factory.md and docs/agents/pull-requests.md in full.

R3. WHEN the diff touches .NET code or tests, Terraform, Kafka client settings, `scenarios/`, `results/` or `.github/workflows/`, THEN the reviewer applies docs/blueprint.md ADRs and the documents that row named before.

R4. WHEN a rule elsewhere in the repository cites a changed file by name, THEN the reviewer applies that rule.

R5 (failure path). WHEN a changed file matches no row but "anything", such as `docs/operations/` prose or `README.md`, THEN step 2 is still met, because "anything" matches every file.

R6 (failure path). WHEN a pull request changes `SKILL.md` itself, THEN its cold review applies the `origin/main` version of the skill under unchanged step 2. The pull request that introduces this rubric is reviewed under the old one.

R7. WHEN the answer to open question 2 names where the measurement of decision 6744694270 point 2 is recorded, THEN that text is added in the file the answer names. This plan pins nothing for it until then.

## Invariants and safety rules

- AGENTS.md GOVERNANCE, which holds the hard safety rules, verdict integrity and the merge rule, applies to every file.
- No severity in AGENTS.md "Code Review Rules" changes.
- Step 2's `origin/main` rule is unchanged, so a diff still cannot weaken the rules it is reviewed against.
- The rubric cites documents and sections and restates no rule (AGENTS.md "Skills").

Consequences of decision 6744694270 point 1, recorded so that no one reads them as accidents. Under the accepted split:
- CLAUDE.md, AGENTS.md "Before opening a pull request" (rules 11 to 13) and AGENTS.md "Skills" are in no row. They apply only through the last row, when they cite a changed file by name.
- docs/blueprint.md ADRs do not apply to a diff that touches only process files or documentation. ADR-018 (reviewer and implementer tools) is one ADR a process diff can contradict.
- Every implementation pull request touches `items/<issue>/state.md` (docs/agents/factory.md "Item state file"), so every implementation pull request still reads factory.md and pull-requests.md in full. For those pull requests the saving is CLAUDE.md and the two AGENTS.md sections above.

## Test map

This is the predicate map of docs/agents/pull-requests.md for this pull request. No verifier predicate changes. Each check is a script, run from the repository root on the pull request head.

| Req | Check | Input | Expected |
|---|---|---|---|
| R1 to R5 | T1: extract the rubric block from SKILL.md, `awk '/^## Rubric$/{f=1;next}/^## Report$/{f=0}f' .agents/skills/repo-review/SKILL.md`, and `diff` it with the fenced block above, taken from this file at its approved commit, with one blank line added before and after | both blocks | empty diff, exit 0; the SKILL.md block has 13 lines: blank, 11 table lines, blank |
| Scope | T2: `git diff --name-only origin/main...HEAD` | pull request head | `.agents/skills/repo-review/SKILL.md`, paths under `items/414/`, and only the files the answers to open questions 1 and 2 add |
| R6 | T3: `diff <(git show origin/main:.agents/skills/repo-review/SKILL.md \| sed '/^## Rubric$/,/^## Report$/d') <(sed '/^## Rubric$/,/^## Report$/d' .agents/skills/repo-review/SKILL.md)` | both versions | empty diff, exit 0 |
| Invariant | T4: `readlink .claude/skills/repo-review` | symlink | `../../.agents/skills/repo-review` |
| R2, R3 | T5: for each path in backticks in the first column of the rubric, `git ls-files -- <path> \| head -1` | the 11 paths | 11 of 11 non-empty |
| Writing | T6: `LC_ALL=C grep -nP '[^\x00-\x7F]' .agents/skills/repo-review/SKILL.md` | changed file | no output, exit 1 |
| Docs | T7: `npm run check:docs` | pull request head | warnings disposed under docs/agents/pull-requests.md rule 8 |

The counts 13 and 11 were computed by script from the block above and from `main` at 6b0194b; T1 and T5 recompute them.

No offline test can show that the narrower rubric still catches what the old one caught. That is a behavioural claim about a model reviewer, and decision 6744694270 point 2 answers it by measurement after merge.

## Deployment assumptions

None. The change is prose read by a reviewer agent; no deployed container, image, chart or runtime depends on it.

## Acceptance commands

1. T1 to T6, with their output recorded in the authored receipt.
2. `npm run check:docs` (T7), with each warning fixed or explained.
3. The cold review under AGENTS.md "Before opening a pull request" rule 8, which applies the `origin/main` skill (R6).
4. Codex review of the head commit under AGENTS.md GOVERNANCE "Merge rule". Hari merges.

## Sources

- Issue #414 body, `updated_at` 2026-11-09T14:01:42Z, and decision comment 6744694270.
- `.agents/skills/repo-review/SKILL.md` on `main` at 6b0194b, lines 6, 10, 11, 15 and 17 to 27.
- AGENTS.md on `main` at 6b0194b, line 111: "A cold reviewer reads it too, through the `repo-review` rubric."
- docs/agents/pull-requests.md on `main` at 6b0194b, line 3: "Read it in full before you start such work, and before you review such work cold."
- docs/agents/pull-requests.md on `main` at 6b0194b, rule 8, last paragraph: the #184 precedent, which records a post-merge measurement as a rule in that file and its results on the issue.
- docs/agents/factory.md on `main` at 6b0194b, "Item state file".
- `.github/CODEOWNERS` on `main` at 6b0194b, line `/.*/`.

## Plan-check questions

(a) Effort that scales with a number unknown at planning time: none in the implementation, which edits one table. At review time, the last row's citations are counted only at runtime. The proposal sets no cap, and this plan adds none.

(b) Stricter than its parent: there is no parent plan. Against the accepted proposal, the plan adds nothing. It pins the reading "those rows already name" as the five existing code rows, which open question 3 asks Hari to confirm.

(c) Checks that depend on code, data or behaviour that is neither on `main` nor in the pull request: none. T1 to T7 read only the repository.

## Open questions for Hari

1. Conflict with two protected files. Under the accepted split, a code-only diff no longer gets docs/agents/pull-requests.md through the rubric. AGENTS.md line 111 says "A cold reviewer reads it too, through the `repo-review` rubric", and pull-requests.md line 3 says to read it in full "before you review such work cold". Its rules 1, 2 and 4 (claims checked at source, counts by script) also bind code pull requests. Should this pull request (a) amend those two sentences, which adds AGENTS.md and pull-requests.md to its scope, (b) leave them and accept the inconsistency, or (c) add pull-requests.md to the "anything" row, which departs from the accepted split?

2. Where the measurement lives. Decision 6744694270 point 2 orders a measurement on the next three pull requests that get a cold review, but does not say where the instruction or the results are recorded. Without a written rule, a later session has no reason to measure. The #184 precedent put the instruction in pull-requests.md rule 8 and the results on the issue. Should this pull request add such a paragraph to pull-requests.md rule 8, with results on issue #414, or should the record go elsewhere? Also: is "run time per round" the harness-reported duration of each cold-review subagent, as the issue's evidence table used?

3. Code without a row. The proposal's "Code" bullet adds ADRs to "those rows", and the current rubric has no row for `charts/`, `infra/scripts/` or `scripts/`. Under this plan, Helm charts, Python and shell scripts match only "anything" and any citing rule, so no ADR is applied to them. Is that intended, or should those paths join a code row?

## Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change the rubric text above, a requirement, or the scope, including any edit to a file outside the scope list.
