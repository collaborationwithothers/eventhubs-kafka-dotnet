# repo-review-scope contract-grade plan (issue #414)

Revision: r1

This plan narrows the cold-review rubric in `.agents/skills/repo-review/SKILL.md` so that a reviewer reads the rule documents a diff's files depend on, not every rule document for every diff. It follows the proposal in issue #414, with three departures that the proposal's own text forces, each listed under "Open questions for Hari". It is not approvable until Hari answers those questions; the plan decides none of them.

## Goal and authority

Goal: replace the rubric's single "anything" row with rows keyed to what the diff touches, as issue #414 "Proposal" describes.

Authority, in creation order:
- The body of issue #414 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414), `updated_at` 2026-11-09T14:01:42Z. It is the only authority. The issue had no comments when this revision was written, so no decision or approval changes it (docs/agents/factory.md "Item lifecycle" step 3).

There is no approved item plan and no parent issue. This is directly authorized governance work under docs/agents/pull-requests.md, the execution-card paragraph: the implementer records the task brief, the issue and the approval of this plan, and claims no item plan.

## Scope

Files allowed to change:
- `.agents/skills/repo-review/SKILL.md`, only between the line `## Rubric` and the line `## Report`.
- `items/414/state.md`, written under docs/agents/factory.md "Item state file".
- One authored receipt under `items/414/`, with a file name the implementer chooses.

Paths that must not change: every other path, including AGENTS.md, CLAUDE.md, docs/agents/factory.md, docs/agents/pull-requests.md, `.claude/agents/repo-reviewer.md`, the symlink `.claude/skills/repo-review`, and the frontmatter and every section of `SKILL.md` other than "Rubric".

`.agents/skills/repo-review/SKILL.md` is a protected path (`.github/CODEOWNERS` line `/.*/`), so Hari merges the pull request (AGENTS.md GOVERNANCE "Merge rule"; AGENTS.md "Skills", "change only through a pull request to Hari").

## External contracts

The rubric table is published wording that every later cold review reads. After the change, the text from the line after `## Rubric` up to the line before `## Report` is exactly the block below, byte for byte, with one blank line before and after the table.

```text
| The diff touches | Apply |
|---|---|
| anything | AGENTS.md: all of GOVERNANCE (scope and deferred items included), "Code Review Rules", writing standards; docs/agents/pull-requests.md in full; the originating specification identified in the receipt |
| `AGENTS.md`, `CLAUDE.md`, `docs/agents/`, `.agents/skills/`, `.claude/`, `.codex/` | AGENTS.md: "Before opening a pull request", "Skills"; CLAUDE.md; docs/agents/factory.md in full; docs/blueprint.md ADRs |
| `items/` | docs/agents/factory.md "Authority reads", "Item state file" and "Item lifecycle" step 3 |
| `docs/engineering/` | each changed document itself |
| `docs/blueprint.md` | docs/blueprint.md ADRs |
| .NET code or tests | docs/engineering/dotnet.md; docs/blueprint.md ADRs |
| Terraform | docs/engineering/terraform.md; docs/blueprint.md ADRs, ADR-011 included |
| other code, scripts or charts (`charts/`, `infra/scripts/`, `scripts/`) | docs/blueprint.md ADRs |
| Kafka client settings | docs/blueprint.md "Workloads (.NET 10, Confluent.Kafka 2.16.0 pinned)": every setting traceable to an inventory item; docs/blueprint.md ADRs |
| `scenarios/`, `results/` | AGENTS.md verdict integrity; docs/blueprint.md ADRs |
| `.github/workflows/` | AGENTS.md safety rules 8 and 9; docs/blueprint.md ADRs |
| a file whose repository path, or whose file name when no other tracked file has that name, appears in a document on `origin/main` (`git grep -n -F`) | each rule containing that mention, read with its enclosing section |
```

No other external contract changes. Step 2 of the skill, which reads the rows and applies `origin/main` versions, stays as it is.

## Requirements

R1. WHEN a cold reviewer applies the changed skill to any file in scope, THEN it applies the "anything" row, which includes docs/agents/pull-requests.md in full.

R2. WHEN the diff touches `AGENTS.md`, `CLAUDE.md`, or any path under `docs/agents/`, `.agents/skills/`, `.claude/` or `.codex/`, THEN the reviewer also applies the process row. The "anything" row and the process row together name every document the current "anything" row names, and no fewer.

R3. WHEN the diff touches a path under `items/` and no process path, THEN the reviewer applies the "anything" row and the `items/` row, and the rubric names neither CLAUDE.md, nor AGENTS.md "Skills", nor docs/blueprint.md ADRs for that file.

R4. WHEN the diff touches .NET code or tests, Terraform, Kafka client settings, `scenarios/`, `results/`, `.github/workflows/`, `charts/`, `infra/scripts/`, `scripts/` or `docs/blueprint.md`, THEN its row names docs/blueprint.md ADRs, plus the documents the current row already names.

R5. WHEN a changed file's repository path, or its file name where that name is unique among tracked files, appears in a document on `origin/main`, THEN the reviewer applies each rule containing that mention.

R6 (failure path). WHEN the citation search for a changed file finds no mention, THEN the last row does not match that file, and step 2 is still met because the "anything" row matches every file.

R7 (failure path). WHEN a pull request changes `SKILL.md` itself, THEN its own cold review applies the `origin/main` version of the skill under the unchanged step 2, so the narrowed rubric does not review its own introduction.

## Invariants and safety rules

- Every document the current "anything" row names stays reachable through at least one row (R2).
- AGENTS.md GOVERNANCE, which holds the hard safety rules, verdict integrity and the merge rule, applies to every file.
- No AGENTS.md "Code Review Rules" severity changes.
- Step 2's `origin/main` rule is unchanged, so a diff still cannot weaken the rules it is reviewed against.
- No rule is restated in the skill. The rubric cites documents and sections; AGENTS.md "Skills" bars paraphrasing a rule that stops, caps or gates work.

## Test map

This is the predicate map of docs/agents/pull-requests.md for this pull request. No verifier predicate changes. Each check is a script; run from the repository root on the pull request head.

| Req | Check | Input | Expected |
|---|---|---|---|
| R1-R6 | T1: extract the rubric block from SKILL.md and from this plan file at its approved commit, then `diff` them | `awk '/^## Rubric$/{f=1;next}/^## Report$/{f=0}f' .agents/skills/repo-review/SKILL.md` against the fenced block above | empty diff, exit 0; the extracted SKILL.md block has 16 lines: blank, 14 table lines, blank |
| Scope | T2: `git diff --name-only origin/main...HEAD` | pull request head | exactly `.agents/skills/repo-review/SKILL.md` and paths under `items/414/` |
| Scope | T3: `diff <(git show origin/main:.agents/skills/repo-review/SKILL.md \| sed '/^## Rubric$/,/^## Report$/d') <(sed '/^## Rubric$/,/^## Report$/d' .agents/skills/repo-review/SKILL.md)` | both versions | empty diff, exit 0 |
| Invariant | T4: `readlink .claude/skills/repo-review` | symlink | `../../.agents/skills/repo-review` |
| R2 | T5: every document and section named in the current "anything" row (`git show origin/main:.agents/skills/repo-review/SKILL.md`, line 21) appears in the new "anything" row or the process row, by `grep -F` of each of these 9 strings: `GOVERNANCE`, `"Code Review Rules"`, `"Before opening a pull request"`, `"Skills"`, `writing standards`, `docs/agents/pull-requests.md`, `CLAUDE.md;`, `docs/agents/factory.md`, `docs/blueprint.md ADRs` | the two rows | 9 of 9 found |
| R4 | T6: each path in backticks in the first column of every rubric row except the last matches at least one tracked file, by `git ls-files -- <path> \| head -1` | the 15 paths | 15 of 15 non-empty |
| Writing | T7: `LC_ALL=C grep -nP '[^\x00-\x7F]' .agents/skills/repo-review/SKILL.md` | changed file | no output, exit 1 |
| Docs | T8: `npm run check:docs` | pull request head | warnings disposed under docs/agents/pull-requests.md rule 8 |

The counts 16, 9 and 15 were computed by script from the block above and from `origin/main` at 6b0194b; T1, T5 and T6 recompute them. R7 needs no test: it is unchanged step 2 text that T3 holds fixed.

No offline test can show that the narrower rubric still catches the findings it caught before. That is a behavioural claim about a model reviewer. It is open question 4.

## Deployment assumptions

None. The change is prose read by a reviewer agent; no deployed container, image, chart or runtime depends on it.

## Acceptance commands

1. T1 to T7 above, with their output recorded in the authored receipt.
2. `npm run check:docs`, with each warning fixed or explained.
3. The cold review under AGENTS.md "Before opening a pull request" rule 8, which applies the `origin/main` version of the skill (R7).
4. Codex review of the head commit under AGENTS.md GOVERNANCE "Merge rule". Hari merges.

## Sources

- Issue #414 body, read 2026-11-09 at `updated_at` 2026-11-09T14:01:42Z.
- `.agents/skills/repo-review/SKILL.md` on `main` at 6b0194b, lines 11 and 17 to 27.
- AGENTS.md on `main` at 6b0194b: line 111, "A cold reviewer reads it too, through the `repo-review` rubric"; "Skills"; GOVERNANCE "Merge rule".
- docs/agents/pull-requests.md on `main` at 6b0194b, line 3: "Read it in full before you start such work, and before you review such work cold."
- docs/agents/factory.md on `main` at 6b0194b, "Item state file": "The implementing session ... creates or updates the file in its implementation pull request".
- `items/` on `main` at 6b0194b: `items/355/receipt.md`, `items/373/receipt.md` and `items/404/pr2-receipt.md` are receipts committed in implementation pull requests.
- `.github/CODEOWNERS` on `main` at 6b0194b, line `/.*/`.

## Plan-check questions

(a) Effort that scales with a number unknown at planning time: none in the implementation, which edits one table. At review time, the last row's citation search returns a number of mentions known only at runtime. The plan sets no cap on it; open question 5 asks whether one is needed.

(b) Stricter than its parent: there is no parent plan. Against the issue's proposal, the plan is stricter in three places: docs/agents/pull-requests.md applies to every file (open question 1), the process row keeps every document of the current row (open question 2), and the code rows name `charts/`, `infra/scripts/`, `scripts/` and `docs/blueprint.md` (open question 3). It is looser in one: `items/` gets a narrow row instead of the full process read (open question 2).

(c) Checks that depend on code, data or behaviour that is neither on `main` nor in the pull request: none. T1 to T8 read only the repository.

## Open questions for Hari

1. The proposal's "Always" row omits docs/agents/pull-requests.md. AGENTS.md line 111 and pull-requests.md line 3 say a cold reviewer reads that file in full, and the skill's steps 1 and 6 apply its rules 5 and 8 to every review. Its rules 1, 2 and 4 (claims verified at source, counts by script) also apply to code pull requests. This plan puts it in the "anything" row. The alternative is to follow the proposal and amend AGENTS.md line 111 and pull-requests.md line 3 in the same pull request, which widens scope to two more protected files. Which do you want?

2. Every implementation pull request writes `items/<issue>/state.md` (factory.md "Item state file") and usually a receipt under `items/`. With `items/` in the process row, as the proposal lists it, every implementation pull request gets the full read, and the change saves nothing for them. This plan gives `items/` its own narrow row. The proposal's "Risk" section also says process files "keep the full read", but its process row names only factory.md and pull-requests.md; one of #413's three P1s rested on AGENTS.md "Skills", which that row omits. This plan's process row keeps every document of the current row. Do you accept both choices, or answer your issue's question 1 another way?

3. The proposal's code row says "code" without paths. The current rubric has no row for `charts/`, `infra/scripts/`, `scripts/` or `docs/blueprint.md`. This plan adds them with the blueprint ADRs. Documentation outside these rows, such as `docs/operations/` and README.md, gets only the "anything" row and any citing rule. Is that the split you want?

4. Your issue's question 2: should the change be measured on the next three pull requests (run time per round, P0 and P1 escapes) before it becomes permanent? If yes, say where the record goes and what result reverts the change. This plan adds no measurement, because the issue leaves it open.

5. The citation row has no cap on the number of mentions a reviewer follows. A common path such as `AGENTS.md` is mentioned in many files. Should the row apply only to mentions in AGENTS.md, CLAUDE.md, docs/agents/ and docs/engineering/, or stay repository-wide?

## Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change the rubric text above, a requirement, or the scope, including any edit to a file outside the scope list.
