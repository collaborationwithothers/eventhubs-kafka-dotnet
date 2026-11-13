# Plan: let standing approval apply checker-supplied, non-design plan fixes

Issue: [#461](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/461). Pull request name: `apply-in-revision`. This is the contract-grade plan for the issue's single pull request under docs/agents/factory.md "Item lifecycle" step 3.

Revision 1. Base: `main` at 1c2ed5e618a0a108e69ca73f4591059e68619fe2. Line numbers below are on that commit.

## 1. Goal and authority

Under standing approval, a plan whose newest check lists any blocking finding hands off to Hari, even when the check supplies the exact fix and calls it not design-changing. This pull request adds a plan-check marker, `apply in revision`, for such findings, and lets the implementing session under standing approval apply them, get a fresh check and continue, at most twice per plan. Every other blocking finding, and every open question, still goes to Hari.

Authorities, in creation order (docs/agents/factory.md "Item lifecycle" step 3):

1. The issue body (created 2026-11-13T16:38:02Z, `updated_at` 2026-11-13T16:38:34Z). Its "Proposal" items 1 to 5 define the work; its "Counterargument" states that "A Codex check still cannot approve under standing approval (factory.md step 3), so this changes nothing for Codex-checked plans"; its "Files likely touched" names `docs/agents/factory.md` "Item lifecycle" steps 1 and 3, `.agents/skills/plan-check/SKILL.md` and `.agents/skills/implement-plan/SKILL.md`.

`ready-for-agent` was applied at 2026-11-13T16:38:34Z (issue events). The issue had no comments when this plan was written, so no comment names the implementing tool: Claude Code by default (docs/agents/factory.md "Item lifecycle" step 2). There is no item state file (`items/461/` does not exist on the base), no approved item plan, no parent issue and no split. The evidence links in the issue body (#445, #443, #452, #440) are evidence, not authority; no decision on this issue adopts them, and this plan relies on none of their text.

## 2. Scope

Files this pull request may change:

- `docs/agents/factory.md`: line 5 (status line), line 49 (implementation row of "Authority reads"), line 101 ("Item lifecycle" step 1), line 111 (first sentence of the planning-session bullet of step 3) and line 114 (the "Standing approval" bullet of step 3), only as section 3 pins.
- `.agents/skills/plan-check/SKILL.md`: line 21 (step 6) only.
- `.agents/skills/implement-plan/SKILL.md`: line 16 (step 1) only.
- `items/461/`: this plan, the authored receipt `items/461/receipt.md`, and the item state file `items/461/state.md` under docs/agents/factory.md "Item state file".

Must not change: any other line of the files above; `AGENTS.md`, `CLAUDE.md`, `docs/agents/pull-requests.md`; `.agents/skills/plan-pr/SKILL.md`, `.agents/skills/repo-review/`; `.claude/agents/`, `.codex/agents/`; `.claude/skills/` (symlinks; they follow `.agents/skills/` without change); `docs/operations/factory-overview/`; `.github/`; `src/`, `tests/`, `scripts/`, `scenarios/`, `results/`, `infra/`; anything under `items/` except `items/461/`.

Protected paths: `/docs/` and `/.*/` are listed in `.github/CODEOWNERS` (lines 5 and 7 on the base), so every change except `items/461/` needs Hari's merge (AGENTS.md GOVERNANCE "Merge rule"). The issue body says the same: "All protected."

Not changed, on purpose: the planning session's own handling of check findings outside standing approval (`.agents/skills/plan-pr/SKILL.md` step 5, and Hari-approved plans under step 3) stays as it is. The issue's proposal 2 limits the new behaviour to standing approval.

## 3. External contracts

These are the published rule texts other sessions read. Each is pinned literally. "Append" means: add one space after the last character of the existing text named, then the literal text.

### 3.1 The marker (factory.md "Item lifecycle" step 1, line 101)

Append, after the line's last sentence `Basis: [#184 decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6397456399), A1.`, literal:

```
A plan check may mark a blocking finding `apply in revision` only when all of these hold: the check states that the fix is not design-changing under this step; for each place the fix changes, it names the plan file section and gives the literal replacement text, or the literal text to delete; and the fix changes no gate, contract, requirement, scope, policy value or verdict rule, and adds, removes or answers no open question. The marker is the literal text `apply in revision` on the finding's first line under `Blocking findings:`, and the check explains why each condition holds. A marked finding is still a blocking finding; only step 3 "Standing approval" acts on the marker, and only for the plan revision identified by that check. Basis: [#461](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/461) proposals 1 and 4.
```

### 3.2 The exception (factory.md "Item lifecycle" step 3 "Standing approval", line 114)

Replace the sentence, literal and occurring once in the file on the base:

```
A current plan whose newest check lists blocking findings is not revised this way; the session hands off.
```

with, literal:

```
A current plan whose newest check lists blocking findings is not revised this way; the session hands off, except as follows. When the newest check identifies the current plan revision under step 1, is not by a Codex subagent, and marks every blocking finding `apply in revision` under step 1, the plan's "Open questions for Hari" states `None`, and fewer than 2 revisions of this plan have been made under this exception, the session itself makes one revision. It applies exactly the marked texts, as a new commit on the plan branch that changes only the plan file, at the places the findings name and, where the file has them, its revision label and its list of changes since the previous revision; it checks by script that the commit changes nothing else. It edits the plan comment under the planning bullet above. The comment lists, under the line `Applied check findings:`, each applied finding with a link to its check, under the new revision label; this and any later edit of the comment keep the lists of earlier revisions made under this exception. The number of revision labels under `Applied check findings:` is the count for the cap. It then spawns a new fresh-context check subagent, as above, whether or not step 1 requires a new check, and selects again. When the cap is reached, a blocking finding is not marked, or an open question remains, the session hands off. Basis: [#461](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/461) proposals 2 to 5.
```

### 3.3 Who writes the revision (factory.md "Item lifecycle" step 3, line 111)

Append, after the line's first sentence `A planning session, or a planning subagent under "Standing approval" (next bullet), writes or revises a contract-grade plan for one pull request.` and before `It edits no file on`, literal (followed by one space before `It edits`):

```
The implementing session under that bullet revises a plan only as its `apply in revision` exception allows, and follows the rest of this paragraph for that revision.
```

### 3.4 What the implementing session reads (factory.md "Authority reads", line 49)

In the implementation row, after the text `the item state file and its catch-up, and every file the plan changes.` and before the closing ` |`, insert one space and then, literal:

```
Under "Item lifecycle" step 3 "Standing approval", when no plan is approved, also the current plan comment, the plan file at the commit it names, and the newest `## Plan check` that identifies that revision, for its `apply in revision` findings ("Item lifecycle" step 1).
```

### 3.5 Amendment record (factory.md line 5)

Append, after the line's last sentence (ending `read-only plan checks and re-checks only for design changes.`), literal, with `<YYYY-MM-DD>` replaced by the UTC date the pull request opens:

```
Amended in the pull request for issue #461 (<YYYY-MM-DD>): "Authority reads" and "Item lifecycle" steps 1 and 3, `apply in revision` findings under standing approval.
```

### 3.6 plan-check skill (`.agents/skills/plan-check/SKILL.md` line 21, step 6)

Append, after the step's last sentence `For a contract-grade plan file, name the plan file path and commit SHA you checked; for a plan held as a comment, identify it as that format requires.`, literal:

```
Mark a blocking finding `apply in revision` only as factory.md "Item lifecycle" step 1 allows, with what that step requires for it.
```

### 3.7 implement-plan skill (`.agents/skills/implement-plan/SKILL.md` line 16, step 1)

Replace the sentence, literal:

```
If selection has no candidate, apply factory.md "Item lifecycle" step 3 "Standing approval" before handing off.
```

with, literal:

```
If selection has no candidate, apply factory.md "Item lifecycle" step 3 "Standing approval", including its `apply in revision` exception, before handing off.
```

## 4. Requirements

Each requirement states what the changed rule text makes a session do; section 3 pins the text that carries it.

1. WHEN a plan check finds a blocking finding whose fix is not design-changing under step 1, gives the literal replacement or deletion text for each changed place, and changes no gate, contract, requirement, scope, policy value, verdict rule or open question THEN it may mark that finding `apply in revision` (3.1, 3.6).
2. WHEN any of the conditions in requirement 1 fails THEN the finding is not marked, and it is handled as today (3.1).
3. WHEN a finding is marked `apply in revision` THEN it still counts as a blocking finding, so the check still lists it under `Blocking findings:` and does not state `Blocking findings: none` (3.1).
4. WHEN, under standing approval, the newest check identifies the current revision, is not by a Codex subagent, marks every blocking finding `apply in revision`, the plan's open questions state `None`, and fewer than 2 revisions were made under the exception THEN the implementing session applies exactly the marked texts as one new commit on the plan branch, edits the plan comment with the `Applied check findings:` list, spawns a new fresh-context check subagent and selects again (3.2, 3.3, 3.7).
5. WHEN the fresh check of requirement 4 states `Blocking findings: none` and the other approval conditions of the "Standing approval" bullet hold THEN the revision is approved under that bullet, unchanged (3.2 leaves those conditions as they are).
6. WHEN the fresh check again marks every blocking finding and the cap is not reached THEN requirement 4 applies again (3.2).
7. WHEN 2 revisions were already made under the exception and the newest check lists any blocking finding THEN the session hands off to Hari (3.2; issue proposal 3).
8. WHEN any blocking finding of the newest check is not marked, or the plan's open questions are not `None` THEN the session hands off, as today (3.2; issue proposal 4).
9. WHEN the newest check is by a Codex subagent THEN the exception does not apply and the session hands off, as today (3.2; issue "Counterargument").
10. WHEN the applying commit changes anything in the plan file beyond the named places, the revision label and the changes list, or any other file THEN the session's script check fails and it does not edit the plan comment or spawn a check (3.2).
11. WHEN any session later edits a plan comment that has `Applied check findings:` lists THEN it keeps those lists (3.2).
12. WHEN the implementing session needs the check's marked texts THEN its authority reads include the current plan comment, plan file and newest check of the current revision (3.4).

## 5. Invariants and safety rules

- Design-changing fixes, open questions, and gates, contracts, requirements, scope, policy values and verdict rules still reach Hari before implementation (issue proposals 1 and 4; factory.md step 1's design-changing list is unchanged).
- The approval conditions of the "Standing approval" bullet are unchanged: a check of the exact revision from that bullet's check subagent, `Blocking findings: none`, and open questions `None`.
- A Codex check still cannot approve under standing approval, and a Codex check's markers trigger nothing (issue "Counterargument"; factory.md line 113 unchanged).
- A plan revised after it is approved is still an amendment needing Hari's approval (line 114's last sentences unchanged).
- The rule is stated once, in factory.md; the two skills cite it (factory.md "Item lifecycle" step 3, contract-grade definition; AGENTS.md "Skills", last paragraph but one).
- No workflow, Azure, secret, verdict or `results/` change (AGENTS.md hard safety rules 2, 3, 7 and 8; "Verdict integrity").

## 6. Test map

The changes are rule text with no code. Each requirement maps to a scripted text check run on the pull request head (acceptance commands in section 8). docs/agents/pull-requests.md's predicate map does not apply: this is not verifier work.

| Requirement | Check | Input | Literal expected value |
|---|---|---|---|
| 1, 2, 3 | A1 | `docs/agents/factory.md` | the text block of 3.1 occurs exactly once, on the line that begins `1. **Plan.**` |
| 4 to 10 | A2 | `docs/agents/factory.md` | the replacement block of 3.2 occurs exactly once, on the line that begins `   - Standing approval (#104).`, and the old sentence of 3.2, with its final full stop, occurs zero times |
| 4, 11 | A3 | `docs/agents/factory.md` | the text of 3.3 occurs exactly once, on the line that begins `   - A planning session, or a planning subagent` |
| 12 | A4 | `docs/agents/factory.md` | the text of 3.4 occurs exactly once, on the line that begins `| Implementation (\`implement-plan\`) |`, and that line still ends with ` |` |
| record | A5 | `docs/agents/factory.md` | the text of 3.5, with a date matching `[0-9]{4}-[0-9]{2}-[0-9]{2}`, occurs exactly once, on line 5 |
| 1 | A6 | `.agents/skills/plan-check/SKILL.md` | the text of 3.6 occurs exactly once, on the line that begins `6. Post the findings` |
| 4 | A7 | `.agents/skills/implement-plan/SKILL.md` | the new text of 3.7 occurs once and the old sentence of 3.7 occurs zero times |
| scope | A8 | `git diff --name-only origin/main...HEAD` | only `docs/agents/factory.md`, `.agents/skills/plan-check/SKILL.md`, `.agents/skills/implement-plan/SKILL.md` and paths under `items/461/` |
| scope | A9 | `git diff -U0 origin/main...HEAD` on the three non-item files | changed lines are exactly factory.md lines 5, 49, 101, 111 and 114, plan-check line 21 and implement-plan line 16; each new line equals its base line with only the section 3 insertions or replacements applied |
| style | A10 | the three non-item files and `items/461/` | no byte outside printable ASCII, tab and newline in added lines (AGENTS.md "Writing standards") |

Existing tests: none read these files' text. `scripts/tests/docs-check.test.mjs` uses its own fixture paths (lines 114 to 116 on the base) and `tests/Parchwell.Scenarios.Tests/ScenarioLoaderTests.cs` line 137 cites factory.md in a comment only. The plan adds no code check, so the plan-pr skill's existing-test fixture rule has no case here.

## 7. Deployment assumptions

None. The change is rule text; no deployed container, image, chart or runtime is involved.

## 8. Acceptance commands

Run on the pull request branch after the changes, before the cold review, and record each output in `items/461/receipt.md`:

1. A1 to A7 and A10: one script, kept in the receipt or run inline, that reads each file, counts each literal of section 3 by exact string match, and prints one `PASS <id>` or `FAIL <id>: <reason>` line per check. Evidence: eight `PASS` lines (A1 to A7 and A10), and exit status 0.
2. A8: `git fetch origin main && git diff --name-only origin/main...HEAD`. Evidence: the printed list, matching the A8 set.
3. A9: `git diff -U0 origin/main...HEAD -- docs/agents/factory.md .agents/skills/plan-check/SKILL.md .agents/skills/implement-plan/SKILL.md`, with a script that reapplies section 3's edits to the base text and compares the result to the head files byte for byte. Evidence: `identical` for each of the three files.
4. `npm run check:docs`. Evidence: the summary line; each warning in a changed file fixed or explained in the pull request under docs/agents/pull-requests.md rule 8, others in one blanket line.
5. Verification gates: docs/agents/pull-requests.md "Verification gates" steps 1 and 3 do not apply, because no file under `src/`, `tests/` or the other paths its last paragraph lists changes. Step 2 (cold review under rule 8) applies, and step 4 (hosted `.NET verification` on the final head) runs as the required check.

## 9. Sources

1. [Issue #461 body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/461), read 2026-11-13, `updated_at` 2026-11-13T16:38:34Z.
2. `docs/agents/factory.md` at 1c2ed5e618a0a108e69ca73f4591059e68619fe2: line 5, line 49, line 101 (step 1: design-changing list, `apply in implementation`, check-comment format), lines 109 to 115 (step 3: selection, planning bullet, checker, "Standing approval", implementation).
3. `.agents/skills/plan-check/SKILL.md` and `.agents/skills/implement-plan/SKILL.md` at the same commit, lines 21 and 16.
4. `docs/agents/pull-requests.md` at the same commit: rule 8 (plan-branch pushes; docs check), "Verification gates" last paragraph.
5. `.github/CODEOWNERS` at the same commit, lines 5 and 7.

External behaviour (plan-pr "Plan rules" rule 1): none. The plan relies on no Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour, so no documentation tool was needed.

## 10. Plan-check questions (docs/agents/factory.md "Item lifecycle" step 1)

(a) Steps whose effort scales with a number unknown at planning time: none in this pull request; it changes seven lines of rule text. The rule it adds bounds a runtime count itself: at most 2 revisions per plan under the exception, exhaustion action hand off to Hari (3.2; issue proposal 3).

(b) Where the plan is stricter than its authority, and why. There is no parent plan; compared with the issue body:
- The newest check must identify the current revision. Without this, a stale check could drive a revision; step 1 already ties checks to revisions.
- A Codex check's markers do not trigger the exception. The issue's counterargument says the change "changes nothing for Codex-checked plans"; this reads that literally.
- The plan's open questions must state `None` before applying. The issue's proposal 4 hands off on any open question; applying first would spend a revision and a check on a plan that must hand off anyway.
- A marked finding stays a blocking finding. This keeps every existing reader of `Blocking findings: none` correct, including the approval condition.
- A script check that the commit changes nothing but the named places, and that `Applied check findings:` lists survive later edits. These make "applies exactly those texts" (proposal 2) and the per-plan cap (proposal 3) checkable from GitHub alone.
- The applying commit may also update the plan file's revision label and changes list. Contract-grade plan files carry both; without this allowance "exactly the marked texts" would leave the file's label wrong.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: none. Every check in section 6 reads files on the base or in the pull request.

## 11. Open questions for Hari

None.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4 or the scope in section 2.
