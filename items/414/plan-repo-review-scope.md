# repo-review-scope contract-grade plan (issue #414)

Revision: r4

This plan replaces the single "anything" row of the cold-review rubric in `.agents/skills/repo-review/SKILL.md` with rows keyed to what a diff touches. It also adds one paragraph to docs/agents/pull-requests.md rule 8, so that the next three cold-reviewed pull requests measure the change. Both texts are pinned below, and the plan has no open questions.

## Goal and authority

Goal: scope the rubric's reading load to the documents a diff's files depend on (issue #414, "Proposal"), and measure the result before Hari decides whether it stays.

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3):
1. The body of issue #414, `updated_at` 2026-11-09T14:01:42Z: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414
2. Decision comment 6744694270, created 2026-11-09T16:00:54Z: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414#issuecomment-6744694270. It accepts the proposed split and orders a measurement on the next three cold-reviewed pull requests, after which Hari decides.
3. Decision comment 6746218503, created 2026-11-09T17:03:26Z: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414#issuecomment-6746218503. It answers this plan's four open questions of revision r3. Its points 1 and 4 change the accepted split, and it wins where it differs from comment 6744694270.

There is no approved item plan and no parent issue. This is directly authorized governance work: the implementer records the task brief, the issue, both decisions and the approval of this plan in the receipt (docs/agents/pull-requests.md, execution-card paragraph), and claims no item plan.

## Scope

Files allowed to change:
- `.agents/skills/repo-review/SKILL.md`, only between the line `## Rubric` and the line `## Report`.
- `docs/agents/pull-requests.md`, only by inserting the paragraph pinned below.
- `items/414/state.md`, written under docs/agents/factory.md "Item state file".
- One authored receipt under `items/414/`, with a file name the implementer chooses.

Paths that must not change: every other path, including AGENTS.md, CLAUDE.md, docs/agents/factory.md, `.claude/agents/repo-reviewer.md`, the symlink `.claude/skills/repo-review`, the frontmatter and every section of `SKILL.md` other than "Rubric", and every existing line of docs/agents/pull-requests.md.

Both changed rule files are protected paths (`.github/CODEOWNERS`, lines `/.*/` and `/docs/`), so Hari merges (AGENTS.md GOVERNANCE "Merge rule").

## External contracts

### C1. Rubric

After the change, the text from the line after `## Rubric` up to the line before `## Report` is exactly the block below, byte for byte, with one blank line before and after the table.

```text
| The diff touches | Apply |
|---|---|
| anything | AGENTS.md: all of GOVERNANCE (scope and deferred items included), "Code Review Rules", writing standards; docs/agents/pull-requests.md in full; the originating specification identified in the receipt |
| `AGENTS.md`, `CLAUDE.md`, `docs/agents/`, `.agents/skills/`, `.claude/`, `.codex/` | AGENTS.md in full; CLAUDE.md; docs/agents/factory.md in full |
| `items/` | docs/agents/factory.md in full |
| `docs/engineering/` | each changed document itself |
| .NET code or tests | docs/engineering/dotnet.md; docs/blueprint.md ADRs |
| Terraform | docs/engineering/terraform.md; ADR-011; docs/blueprint.md ADRs |
| Kafka client settings | docs/blueprint.md "Workloads (.NET 10, Confluent.Kafka 2.16.0 pinned)": every setting traceable to an inventory item; docs/blueprint.md ADRs |
| `scenarios/`, `results/` | AGENTS.md verdict integrity; docs/blueprint.md ADRs |
| `.github/workflows/` | AGENTS.md safety rules 8 and 9; docs/blueprint.md ADRs |
| `charts/`, `infra/scripts/`, `scripts/` | docs/blueprint.md ADRs |
| a file that a rule elsewhere in the repository cites by name | the citing rule |
```

How the rows map to the authorities:
- "anything": the proposal's "Always" bullet, plus docs/agents/pull-requests.md (decision 6746218503 point 1).
- The two process rows: decision 6746218503 point 4. docs/agents/pull-requests.md, which that point names for both rows, applies through "anything" and is not repeated.
- The five code rows from `.NET code or tests` to `.github/workflows/`: the proposal's "Code" bullet, which adds the blueprint ADRs to the rows that existed before.
- The `charts/`, `infra/scripts/`, `scripts/` row: decision 6746218503 point 3.
- The last row: the proposal's last bullet.
- `docs/engineering/`: unchanged.

### C2. Measurement paragraph

In docs/agents/pull-requests.md, insert one blank line and then the line below directly after line 38 on `main` at 6b0194b (the paragraph that starts "Count the first five pull requests merged after the #184 pull request merged"), and before the line that starts "9. Fill in the pull request template". The line starts with three spaces, as the other paragraphs of rule 8 do.

```text
   Measure the cold-review rubric changed for [issue #414](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414) on the first three pull requests that get a cold review after that change merges. For each one, record in its description every cold-review round with its mode and the run time its tool reports, or `not reported` when the tool reports none, and every P0 or P1 that Codex or Hari raised after a cold-review round on a line that round covered. After the third of them has merged, record that evidence per pull request, with links, on issue #414. Hari then decides whether the rubric stays. No result is set in advance that reverts it.
```

The rest of the skill, including step 2's rule that a changed rule document is applied at its `origin/main` version, is unchanged.

## Requirements

R1. WHEN a cold reviewer applies the changed skill to any file in scope, THEN it applies the "anything" row, including docs/agents/pull-requests.md in full.

R2. WHEN the diff touches `AGENTS.md`, `CLAUDE.md`, or a path under `docs/agents/`, `.agents/skills/`, `.claude/` or `.codex/`, THEN the reviewer also applies AGENTS.md in full, CLAUDE.md and docs/agents/factory.md in full.

R3. WHEN the diff touches a path under `items/` and no path of R2, THEN the reviewer also applies docs/agents/factory.md in full, and the rubric names neither CLAUDE.md nor AGENTS.md beyond the "anything" row for that file.

R4. WHEN the diff touches .NET code or tests, Terraform, Kafka client settings, `scenarios/`, `results/`, `.github/workflows/`, `charts/`, `infra/scripts/` or `scripts/`, THEN the reviewer applies docs/blueprint.md ADRs, plus what that row names besides.

R5. WHEN a rule elsewhere in the repository cites a changed file by name, THEN the reviewer applies that rule.

R6 (failure path). WHEN a changed file matches no row but "anything", such as `docs/operations/` prose or `README.md`, THEN step 2 is still met, because "anything" matches every file.

R7 (failure path). WHEN a pull request changes `SKILL.md` or docs/agents/pull-requests.md, THEN its cold review applies their `origin/main` versions under unchanged step 2. The pull request that makes this change is reviewed under the old rubric and is not one of the three measured pull requests.

R8. WHEN the pull request for this plan has merged, THEN docs/agents/pull-requests.md rule 8 holds the C2 paragraph, which binds the next three pull requests that get a cold review.

## Invariants and safety rules

- AGENTS.md GOVERNANCE, which holds the hard safety rules, verdict integrity and the merge rule, applies to every file.
- No severity in AGENTS.md "Code Review Rules" changes.
- Step 2's `origin/main` rule is unchanged, so a diff still cannot weaken the rules it is reviewed against.
- The rubric cites documents and sections and restates no rule (AGENTS.md "Skills").
- AGENTS.md line 111 ("A cold reviewer reads it too, through the `repo-review` rubric") and docs/agents/pull-requests.md line 3 stay true, because the "anything" row names docs/agents/pull-requests.md.

Consequences, recorded so that no one reads them as accidents:
- docs/blueprint.md ADRs no longer apply through the rubric to a diff that touches only process files, `items/` or documentation. The skill's unchanged step 5 still requires checking every change against the rest of the repository, so a contradiction with an ADR remains a finding.
- A diff that touches code and `items/` reads AGENTS.md beyond the "anything" row and CLAUDE.md only through step 5 or the last row.
- How much reading the change saves depends on how reviewers carry out step 5. The C2 measurement observes it.

## Test map

This is the predicate map of docs/agents/pull-requests.md for this pull request. No verifier predicate changes. Each check is a script, run from the repository root on the pull request head. In the table, `\|` is a Markdown escape; run it as `|`.

| Req | Check | Input | Expected |
|---|---|---|---|
| R1 to R6 | T1: extract the rubric block from SKILL.md, `awk '/^## Rubric$/{f=1;next}/^## Report$/{f=0}f' .agents/skills/repo-review/SKILL.md`, and `diff` it with the C1 block, taken from this file at its approved commit, with one blank line added before and after | both blocks | empty diff, exit 0; the SKILL.md block has 15 lines: blank, 13 table lines, blank |
| R8 | T2: `diff <(git show origin/main:docs/agents/pull-requests.md) docs/agents/pull-requests.md` | pull request head | exactly one hunk, `38a39,40`, adding a blank line and the C2 line byte for byte |
| Scope | T3: `git diff --name-only origin/main...HEAD`, on a branch started from `origin/main` | pull request head | `.agents/skills/repo-review/SKILL.md`, `docs/agents/pull-requests.md`, and paths under `items/414/` only |
| R7 | T4: `diff <(git show origin/main:.agents/skills/repo-review/SKILL.md \| sed '/^## Rubric$/,/^## Report$/d') <(sed '/^## Rubric$/,/^## Report$/d' .agents/skills/repo-review/SKILL.md)` | both versions | empty diff, exit 0 |
| Invariant | T5: `readlink .claude/skills/repo-review` | symlink | `../../.agents/skills/repo-review` |
| R2 to R4 | T6: for each path in backticks in the first column of the rubric, `git ls-files -- <path> \| head -1` | the 14 paths | 14 of 14 non-empty |
| Writing | T7: `LC_ALL=C grep -nP '[^\x00-\x7F]' .agents/skills/repo-review/SKILL.md docs/agents/pull-requests.md` | changed files | no output, exit 1 |
| Docs | T8: `npm run check:docs` | pull request head | warnings disposed under docs/agents/pull-requests.md rule 8 |

The counts 15, 13, 14 and the line number 38 were computed by script from the blocks above and from `main` at 6b0194b; T1, T2 and T6 recompute them.

No offline test can show that the narrower rubric still catches what the old one caught. That is a behavioural claim about a model reviewer; the C2 measurement answers it after merge, and Hari decides.

## Deployment assumptions

None. The change is prose read by reviewer and implementing agents; no deployed container, image, chart or runtime depends on it.

## Acceptance commands

1. T1 to T7, with their output recorded in the authored receipt.
2. `npm run check:docs` (T8), with each warning fixed or explained.
3. The cold review under AGENTS.md "Before opening a pull request" rule 8, which applies the `origin/main` versions of both changed rule files (R7).
4. Codex review of the head commit under AGENTS.md GOVERNANCE "Merge rule". Hari merges.

## Sources

- Issue #414 body, `updated_at` 2026-11-09T14:01:42Z, and decision comments 6744694270 and 6746218503.
- `.agents/skills/repo-review/SKILL.md` on `main` at 6b0194b, lines 11, 14 and 17 to 27.
- docs/agents/pull-requests.md on `main` at 6b0194b: line 3, "Read it in full before you start such work, and before you review such work cold"; line 38, the #184 measurement paragraph that C2 follows.
- AGENTS.md on `main` at 6b0194b, line 111.
- docs/agents/factory.md on `main` at 6b0194b, "Item state file".
- `.github/CODEOWNERS` on `main` at 6b0194b, lines `/docs/` and `/.*/`.

## Plan-check questions

(a) Effort that scales with a number unknown at planning time: none in the implementation, which edits one table and inserts one paragraph. At review time, the last row's citations and step 5's cross-checks are counted only at runtime; no cap is set, and the C2 measurement observes the cost. The measurement itself is capped at three pull requests.

(b) Stricter than its parent: there is no parent plan. Against the proposal, the plan applies docs/agents/pull-requests.md to every file, AGENTS.md in full and CLAUDE.md to process files other than `items/`, and the ADRs to `charts/`, `infra/scripts/` and `scripts/`. Each comes from decision 6746218503.

(c) Checks that depend on code, data or behaviour that is neither on `main` nor in the pull request: none. T1 to T8 read only the repository.

## Open questions for Hari

None.

## Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change the C1 or C2 text, a requirement, or the scope, including any edit to a file outside the scope list.
