# control-session-skill contract-grade plan

Revision: R3. Issue: [#476](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476). Base commit: `c6d5676a7ce19b1fc16bcf7a577e998f674b024f` (`main`, read 2026-11-14), the merge commit of pull request [#477](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/477).

This plan adds a repository-owned `control-session` skill that gives the control session of a scenario issue its objective, its own limits on acting alone and a check for deferred work in plans, and points to it from docs/agents/factory.md, AGENTS.md and CLAUDE.md. The skill points to the shared "Act or ask" and "Deferred work in plans" rules and to the "Outcome responsibility pilot" that #477 merged into docs/agents/factory.md, and keeps no count of its own, as Hari's decisions of 2026-11-14 on this issue require. Every new sentence is pinned below, and no question is open.

## Changes since R2

R3 applies the two findings that the [plan check of R2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877877684) marked `apply in revision`, and nothing else:

- B1 "Working method": the takeover line cites the `implement-plan` skill step 2 and docs/agents/pull-requests.md rule 8 instead of paraphrasing a rule that caps work (finding 1; section 11 (b) records it).
- E5-add: the approval-record parenthesis names "the paragraph that explains it" instead of "the paragraph after the Haiku row" (finding 2).

Neither change is design-changing under docs/agents/factory.md "Item lifecycle" step 1: each changes no gate, contract, requirement, scope, policy value or verdict rule and adds, removes or answers no open question.

## Changes since R1

R2 applies [decision 6877655997](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877655997), which answers R1's three open questions:

- Base commit moved from `5ac85af17f4b7dbc1a1396932fb3554d363752fb` to `c6d5676a7ce19b1fc16bcf7a577e998f674b024f`, because #477 merged at 2026-11-14T13:00:39Z (decision point 4; R1 open question 3). R0 and A0 now check the cited headings on `main` instead of waiting for #477. Line numbers and the worked-case commit in B1 are re-read at the new base.
- B1 "Measures" no longer holds the spec's own three-item count. It points to docs/agents/factory.md "Measuring the gate", "Outcome responsibility pilot", states that the pilot assessment also counts reversed `## Control action` comments, and lists what the session records on the issue (decision points 1 to 3; R1 open questions 1 and 2).
- B1 "Act or ask": a question to Hari is brought in a `**Decision needed:**` comment (decision point 2).
- Section 9 "Open questions for Hari" states `None`; R1's R11 is removed, because it only governed open questions.
- Section 13 "Deferred work" now lists the pilot assessment, which issue #475's factory text owns, instead of a count this plan defined.
- Size estimate, check:docs evidence and sources re-measured or re-read at the new base.

## 1. Goal and authority

Goal: implement the approved spec of issue #476 (cause 1 of issue #475), as amended by Hari's decisions, as one documentation pull request. Verdict gates do not change.

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3):

1. The issue body (issue `updated_at` 2026-11-14T13:03:36Z at the catch-up).
2. The spec, [comment 6876933581](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6876933581), created and updated 2026-11-14T11:59:27Z. It is the item plan.
3. [`## Approval` 6877196319](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877196319), 2026-11-14T12:22:06Z: Hari approved the spec at `updated_at` 2026-11-14T11:59:27Z, "Tool: Claude Code". `ready-for-agent` was applied at 2026-11-14T12:21:57Z (issue events, page 1).
4. [`## Decision` 6877246303](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877246303), 2026-11-14T12:26:34Z, which links and adopts [#475 decision 6877239716](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/475#issuecomment-6877239716) (`updated_at` 2026-11-14T12:25:58Z). For this issue it says: "Use the single shared act-or-ask rule in `docs/agents/factory.md` when it is available. Keep section 4's control-role permitted actions and restrictions as role-specific limits; do not duplicate the general decision test. Whichever coordinated change lands second replaces its copy with that pointer." It also says that issue #475 owns the plan `Deferred work` section and deferral policy, that the control session classifies each listed item against that policy, and that section 5 "must distinguish preventably deferred premerge preparation from execution that is authorized only after merge", without requiring "a final mutation-acceptance run before merge when existing rules require it on `main` after merge".
5. [`## Decision` 6877655997](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877655997), 2026-11-14T13:03:36Z. It drops spec section 6's own count in favour of docs/agents/factory.md "Measuring the gate", "Outcome responsibility pilot" as merged in #477 (point 1); lists what the control session records, "with no new comment kind": "each act taken alone: a `## Control action` comment", "each question brought to Hari: its `**Decision needed:**` comment", "each deferral it lets through: its classification against the plan's `Deferred work` section", and "each chat intervention by Hari: a `## Control action` comment that quotes it" (point 2); adds that "`## Control action` comments that Hari reversed are counted by the pilot assessment alongside its own measures. The skill states this; it does not change the pilot's text." (point 3); and closes the wait for #477 (point 4).

Other comments on the issue, read as evidence, not authority: [plan comment 6877447930](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877447930) (R1, `updated_at` 2026-11-14T12:44:48Z), which this revision edits, and the [handoff 6877458356](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877458356). The catch-up listed six comments on page 1 of `issues/476/comments?per_page=100`. There is no item state file (`items/476/` does not exist on `main`), and no `## Plan check` comment. No split is approved on the issue, so candidate selection gives the whole issue as one pull request, still named `control-session-skill`. The work is one coherent piece under "Item lifecycle" step 3 "Split by scope": one new skill and the three short entries that name it, which cannot merge separately without dangling references.

Reader subagents: none were spawned. This planning session ran as a subagent without a tool to spawn further subagents, so it read every input itself.

## 2. Scope

Files allowed to change (all protected paths, so Hari merges; AGENTS.md GOVERNANCE "Merge rule"):

| Path | Change |
|---|---|
| `.agents/skills/control-session/SKILL.md` | New file, byte-equal to block B1 |
| `.claude/skills/control-session` | New symlink, target `../../.agents/skills/control-session` |
| `docs/agents/factory.md` | Edits E1 and E2 |
| `AGENTS.md` | Edits E3 and E4 |
| `CLAUDE.md` | Edits E5, E6 and E7 |

Paths that must not change: every other path, including `skills-lock.json` (repository-owned skills are not in it, AGENTS.md line 95), `docs/agents/pull-requests.md`, the "Outcome responsibility" and "Measuring the gate" sections of docs/agents/factory.md (the pilot's text included, decision 6877655997 point 3), every other file under `.agents/skills/` and `.claude/`, `.github/`, `results/` and every `expected.yaml`. The one exception is `items/476/state.md`, which the implementing session writes under docs/agents/factory.md "Item state file"; its content is that section's, not this plan's.

Out of scope (spec section 9, decision 6877239716 point 4, decision 6877655997 points 1 and 3): any verdict gate, cap, review budget or merge rule; the shared act-or-ask rule, the deferred-work rule and the pilot assessment themselves (issue #475); Hari's personal instructions; the CLAUDE.md Haiku row's task list; lighter routes (issue #419); any count, runner or item selection of this issue's own.

## 3. External contracts

These are the strings other sessions and the pilot assessment read. They are pinned exactly.

1. Skill directory name and frontmatter `name`: `control-session`.
2. Skill frontmatter `description`: the line in block B1. Claude Code uses the description "to decide when to apply the skill"; Codex requires `name` and `description` (sources S3, S4).
3. Comment heading: `## Control action`. It carries no authority under docs/agents/factory.md "Item lifecycle" step 3. The pilot assessment counts those Hari reversed (decision 6877655997 point 3).
4. Question marker: `**Decision needed:**` as the first text of the comment, the existing marker of CLAUDE.md "Handoffs"; no new comment kind (decision 6877655997 point 2).
5. factory.md section heading: `## Control session`. AGENTS.md and the skill cite it as docs/agents/factory.md "Control session".
6. CLAUDE.md "Model bindings" row: ``| Control session | `claude-opus-5-5` |``.
7. The ask-list quotation in block B1, which must equal byte for byte the substring of [#404 decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) point 1 that starts `These always wait for Hari` and ends `anything that spends money.` (that comment's `updated_at` 2026-11-09T21:52:11Z, re-read 2026-11-14).
8. The headings B1 cites in docs/agents/factory.md at the base commit: `## Outcome responsibility` (line 41), and under it `### Act or ask` (line 58) and `### Deferred work in plans` (line 64); `## Measuring the gate` (line 234), and under it `### Outcome responsibility pilot` (line 240).

## 4. Pinned text

Each block is copied exactly, without its fence lines. Each edit is an exact replacement on the file at the merge base with `origin/main`; each `Find` string and anchor line occurs exactly once there. The anchors were checked on the base commit.

### B1: `.agents/skills/control-session/SKILL.md`

The file is the block content followed by one newline, and nothing else.

````text B1
---
name: control-session
description: Runs the control session that coordinates one scenario issue for Hari and owns the time to a correct verdict. Use when asked to run, start or take over the control session of an issue.
---

You own the time to a correct verdict for one scenario issue: you are its control session under docs/agents/factory.md "Control session". Before each step, ask: what is the shortest correct path from here, and what will block that path later? Infer what Hari needs from the state of the issue, not only from the literal words of his request. Fix a problem before it bites when "Act or ask" below lets you act, and tell Hari after.

## Inputs

Your prompt names this skill and the issue number only (AGENTS.md "Skills"). Recognize authority on the issue under docs/agents/factory.md "Item lifecycle" step 3.

## Act or ask

Decide whether to act or to ask under docs/agents/factory.md "Outcome responsibility", "Act or ask". This section adds the control role's own limits to that rule.

You act alone only when all three hold:

1. The step only starts, stops or redirects a planning, plan-check, implementation or diagnosis session, dispatches a workflow on `main` that does not reach Azure, or posts a status note.
2. It can be undone before merge.
3. It touches nothing on the ask list.

Whether a workflow reaches Azure is a fact about its file on `main`: read the file in the same session before you dispatch it (AGENTS.md GOVERNANCE "Truth and verification" and hard safety rule 8).

The ask list is Hari's reserved set (money, public claims under `results/`, `expected.yaml` and verdicts, scope, and protected-path merges) plus this list, copied word for word from point 1 of [#404 standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614): "These always wait for Hari, and the affected plan stops: any protocol sequence, gate, policy value, timeout, failure name, verdict rule, expected outcome, anything under `results/`, ADR or design text, safety property under AGENTS.md hard safety rules, a protected path not already decided, a scope change, and anything that spends money." Waiving or changing a gate or cap is also on it.

This skill grants no authority to post `## Approval` or to merge; that still comes only from a decision on the issue (docs/agents/factory.md "Control session"). The stop and hand-off rules of AGENTS.md "Before opening a pull request" rules 8 and 11 and GOVERNANCE "Merge rule" apply unchanged. When you stop, end under AGENTS.md GOVERNANCE "Handoffs".

After acting, post one comment on the issue headed `## Control action`, stating what you did, why, the evidence, and that Hari has not seen it. That heading carries no authority under docs/agents/factory.md "Item lifecycle" step 3.

When you ask, post a comment on the issue that starts with `**Decision needed:**`, and give the point at which waiting starts to cost time with your recommendation.

Worked cases from issue #404:

| Case | Action |
|---|---|
| Plan defers timing of new mutation targets to after merge (#467) | Before approval: act, send the plan back. After approval: the timing is acceptance, so ask Hari the same day with "revise before merge". |
| Small sign-in fix through full process | Act on the route: use docs/agents/factory.md "Item lifecycle" step 3 "Standing approval" where its conditions hold. Ask on the timeout and retry values (ask list). Lighter routes are #419's. |
| Merge with an unresolved P1 (#455) | Act: post a not-ready note with head, checks and Codex state on each new head. It cannot stop Hari's merge. |
| Cold review looping on receipt wording | Ask: the cold-review budget is a gate. Ask after the first paperwork-only round, not when the budget is spent. |
| Mutation run failures (#469, #470) | Act: diagnose at the first failure, start the fix session, and dispatch the rerun on `main`. On `main` at c6d5676a7ce19b1fc16bcf7a577e998f674b024f, `.github/workflows/verifier-c5-mutation.yml` had no `azure/login` step and no `id-token` permission; read it again before each dispatch. |

## Deferred work in plans

Before a plan reaches Hari, read its `Deferred work` section and anything else the plan defers: to a post-merge run, a follow-up issue, a cap exhaustion action `record the gap and continue`, or anything known only after merge. Classify each item under docs/agents/factory.md "Outcome responsibility", including its "Deferred work in plans":

- Preparation on the path to the verdict that could be done before merge: send the plan back to resolve it before the pull request that depends on it merges, with a measured cost or a cap. Hari is not asked.
- Execution that existing rules allow only after merge, such as the mutation acceptance run on `main`: it stays at that stage, with its completion and publication gates. Do not ask for it before merge.
- Not on the path to the verdict: the item may stay deferred, and the approval request lists it in one line with that call.

This catches no item you cannot see.

## Measures

No targets are set and no speed-up will be claimed (AGENTS.md hard safety rule 11). This skill keeps no count of its own: the assessment is the one in docs/agents/factory.md "Measuring the gate", "Outcome responsibility pilot". That assessment also counts, alongside its own measures, the `## Control action` comments that Hari reversed.

So that the assessment can count this role's part, record on the issue, with no other kind of comment:

- each act you take alone: its `## Control action` comment;
- each question you bring to Hari: its `**Decision needed:**` comment;
- each deferral you let through: its classification under "Deferred work in plans" above, one line per item, in the comment that brings the plan to Hari, or in a `## Control action` comment when no comment does;
- each intervention Hari makes in chat: a `## Control action` comment that quotes his words and states that it records his intervention, in place of stating that he has not seen it.

## Working method

From [#347 handover 6874759569](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6874759569) section 3, as method, not new gates:

- Post a comment, then link its ID from the tool result.
- Record only approvals Hari confirmed.
- Fix at the first failure.
- Check the head, every check and Codex state for the exact head before saying "ready".
- Recheck plans against merged code.
- Ask child sessions to use the documentation tools and cite.
- On a takeover, count caps as the `implement-plan` skill step 2 and docs/agents/pull-requests.md rule 8 set.
- The sandbox cannot run `Tier=CGroupV2`, so hosted CI is the full gate (docs/agents/pull-requests.md "Verification gates", steps 3 and 4).
- When challenged, check the source and give one final proposal.

Sessions you start use the model CLAUDE.md "Model bindings" gives their role.
````

### `.claude/skills/control-session`

A Git symlink (mode `120000`) whose target is exactly `../../.agents/skills/control-session`, the same relative form as the existing `.claude/skills/plan-pr` link (`../../.agents/skills/plan-pr`).

### E1: `docs/agents/factory.md`, status paragraph (line 5)

Find:

````text E1-find
Amended in the pull request for issue #419 (2026-11-14): "Item lifecycle" step 3, splits and child issues; Hari's merge of that pull request is the approval.
````

Replace with the same text followed by one space and:

````text E1-add
Amended in the pull request for issue #476 (spec approved 2026-11-14): the new "Control session" section; Hari's merge of that pull request is the approval.
````

### E2: `docs/agents/factory.md`, new section

Insert block B2, then one empty line, immediately before the line `## Execution and review record` (line 154). The empty line already before that heading stays, so B2 has one empty line on each side.

````text B2
## Control session

A control session coordinates one scenario issue for Hari. It implements nothing. The repository-owned `control-session` skill runs it (AGENTS.md "Skills"). Its authority to approve plans or merge comes only from `## Decision` or `## Approval` comments on its issue, recognized under "Item lifecycle" step 3. Basis: the [approved spec](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6876933581) on issue #476, section 2, and its [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877196319).
````

### E3: `AGENTS.md`, "Skills" (line 95)

Find `Four skills are owned by this repository.` and replace it with `Five skills are owned by this repository.`

### E4: `AGENTS.md`, "Skills" (after line 98)

Insert block B4 as a new line immediately after the line that starts ``- `plan-pr`, `plan-check` and `implement-plan` run``, with no empty line between them.

````text B4
- `control-session` runs the control session of docs/agents/factory.md "Control session". The prompt that starts it names the skill and the issue number only.
````

### E5: `CLAUDE.md`, "Model bindings" status (line 9)

Find:

````text E5-find
Hari's merge of that pull request is the approval of the amendment.
````

Replace with the same text followed by one space and:

````text E5-add
The pull request for issue #476 amends this section (the Control session row and the paragraph that explains it); Hari's merge of that pull request is the approval of that amendment.
````

### E6: `CLAUDE.md`, "Model bindings" table (after line 15)

Insert this line immediately after the line ``| Self-review against docs/engineering/ before opening a pull request | `claude-opus-5-5` |``:

````text E6
| Control session | `claude-opus-5-5` |
````

### E7: `CLAUDE.md`, "Model bindings" (before line 20)

Insert block B7, then one empty line, immediately before the line that starts ``A session started with the `create_session` tool``.

````text B7
The Control session row is `claude-opus-5-5` because the role makes judgement calls (the `control-session` skill, "Act or ask" and "Deferred work in plans"). Polling run status, fetching run logs and comparing heads go to `claude-haiku-5-5` subagents only as the Haiku row allows. Whether a cheaper model would do the judgement well enough is not measured.
````

## 5. Requirements

R0. WHEN the implementing session starts, THEN it checks that `docs/agents/factory.md` on current `main` has the line `## Outcome responsibility` with, after it and before the next `## ` heading, the lines `### Act or ask` and `### Deferred work in plans`, and the line `## Measuring the gate` with, after it and before the next `## ` heading, the line `### Outcome responsibility pilot`. IF any of these five headings is missing or renamed, THEN it stops under section 10.

R1. WHEN the pull request is opened, THEN `.agents/skills/control-session/SKILL.md` exists, is a regular file (Git mode `100644`), and is byte-equal to B1 plus one trailing newline.

R2. WHEN the pull request is opened, THEN `.claude/skills/control-session` is a Git symlink (mode `120000`) whose target is `../../.agents/skills/control-session`, and `.claude/skills/control-session/SKILL.md` resolves to the file of R1.

R3. WHEN the pull request is opened, THEN `docs/agents/factory.md` equals its text at the merge base with `origin/main`, with E1 and E2 applied and no other change.

R4. WHEN the pull request is opened, THEN `AGENTS.md` equals its merge-base text with E3 and E4 applied and no other change.

R5. WHEN the pull request is opened, THEN `CLAUDE.md` equals its merge-base text with E5, E6 and E7 applied and no other change.

R6. WHEN the pull request diff is listed against its merge base with `origin/main`, THEN it names exactly the five paths of section 2, plus `items/476/state.md` if the implementing session writes it under docs/agents/factory.md "Item state file".

R7. WHEN a `Find` string of E1, E3 or E5, or an anchor line of E2, E4, E6 or E7, does not occur exactly once in its file at the merge base, THEN the implementer stops under section 10. Any other change merged since the base commit that leaves every anchor unique is applied around, with the pinned text unchanged.

R8. WHEN `npm run check:docs` runs on the pull request head, THEN `.agents/skills/control-session/SKILL.md` has no warning, and each of `docs/agents/factory.md`, `AGENTS.md` and `CLAUDE.md` has the same warning messages, positions aside, as at the merge base. IF any other warning appears in a changed file, THEN the pinned text is at fault: the implementer does not reword it and stops under section 10.

R9. WHEN the added lines are scanned, THEN every character is ASCII (AGENTS.md "Writing standards").

R10. WHEN the ask-list quotation in B1 is compared with the body of comment 6752430614 fetched by `gh api`, THEN it is a byte-equal substring of that body.

## 6. Invariants and safety rules

- No verdict gate, cap, review budget, merge rule or hard safety rule changes. The skill cites AGENTS.md "Before opening a pull request" rules 8 and 11 and GOVERNANCE "Merge rule" and does not paraphrase them (AGENTS.md "Skills", line 100).
- The skill holds no second copy of the general act-or-ask test, of the deferral policy or of the pilot's method; it cites docs/agents/factory.md "Outcome responsibility" and "Measuring the gate" and adds only the control role's limits and records (decisions 6877246303 and 6877655997).
- The pilot's text in docs/agents/factory.md does not change (decision 6877655997 point 3).
- No new comment kind: the skill uses `## Control action` and `**Decision needed:**` only (decision 6877655997 point 2).
- The skill grants no authority to post `## Approval` or to merge, and the `## Control action` heading carries no authority.
- No secret, key, tenant ID or subscription ID is added (hard safety rule 7). No workflow file changes (hard safety rule 8).
- No number is stated that was not measured (hard safety rule 11). B1 states no count, sample size or target.
- `skills-lock.json` is unchanged; the new skill is repository-owned.
- Plan rule 2 (standing fixture rule) does not apply: this plan adds and tightens no check and changes no test.

## 7. Test map

The pull request changes no code and no test; its checks are the acceptance commands of section 8. Each requirement maps to one check, with its literal expected result.

| Req | Check | Input | Expected |
|---|---|---|---|
| R0 | A0 `headings` | `git show origin/main:docs/agents/factory.md` | `OK headings` |
| R1 | A1 `skill-file` | branch file vs B1; `git ls-files -s` | `OK .agents/skills/control-session/SKILL.md`; `OK skill-file mode` (`100644`) |
| R2 | A1 `symlink` | `git ls-files -s .claude/skills/control-session` and `git cat-file -p` of its object | `OK symlink`: mode `120000`, target `../../.agents/skills/control-session` (36 bytes, no newline) |
| R3 | A1 `factory.md` | merge-base text with E1, E2 vs branch | `OK docs/agents/factory.md` |
| R4 | A1 `AGENTS.md` | merge-base text with E3, E4 vs branch | `OK AGENTS.md` |
| R5 | A1 `CLAUDE.md` | merge-base text with E5, E6, E7 vs branch | `OK CLAUDE.md` |
| R6 | A3 `paths` | `git diff --name-only --merge-base origin/main HEAD` | exactly the five paths, plus `items/476/state.md` if written |
| R7 | A1 anchor counts | each `Find` string and anchor line in the merge-base file | count 1 each; any other count makes A1 print `ANCHOR` and exit 1 |
| R8 | A4 `check:docs` | `npm run check:docs` on the merge base and on the head | skill file: no warning; the three edited files: the same messages on both |
| R9 | A5 `ascii` | added lines of the diff | `0` |
| R10 | A1 `quote` | B1 quote vs comment 6752430614 body | `OK quote` |

Boundaries: A0 fails when a heading is missing, or present only outside its parent section. A1 fails on a missing or extra trailing newline in the skill file (exact bytes), on an anchor that occurs zero or two times, and on any extra changed line in the three edited files.

## 8. Acceptance commands

Run from the repository root on the pull request branch, with this plan saved outside the repository at `$PLAN`. Nothing these commands write is committed (docs/agents/pull-requests.md rule 8).

A0:

```python
import subprocess, sys
f = subprocess.run(["git", "show", "origin/main:docs/agents/factory.md"], capture_output=True, text=True, check=True).stdout.split("\n")
def section(h):
    if h not in f: return []
    i = f.index(h)
    j = next((k for k in range(i + 1, len(f)) if f[k].startswith("## ")), len(f))
    return f[i:j]
o, m = section("## Outcome responsibility"), section("## Measuring the gate")
ok = "### Act or ask" in o and "### Deferred work in plans" in o and "### Outcome responsibility pilot" in m
print("OK headings" if ok else "MISSING headings"); sys.exit(0 if ok else 1)
```

Evidence: `OK headings`, exit 0 (R0). The planning session ran it on 2026-11-14 at `origin/main` = `c6d5676a7ce19b1fc16bcf7a577e998f674b024f` and it printed `OK headings`.

A1 (one script; `$C404` is a file holding the `.body` of comment 6752430614 from `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/issues/comments/6752430614 --jq .body`):

```python
import re, subprocess, sys
plan, c404 = sys.argv[1], sys.argv[2]
def run(*a): return subprocess.run(list(a), capture_output=True, text=True, check=True).stdout
BASE = run("git", "merge-base", "HEAD", "origin/main").strip()
text = open(plan, encoding="utf-8").read()
blocks = dict((m.group(1), m.group(2)) for m in re.finditer(r"````text (\S+)\n(.*?)\n````", text, re.S))
def base(p): return run("git", "show", f"{BASE}:{p}")
def once(s, a, p):
    if s.count(a) != 1: print("ANCHOR", p, repr(a[:60])); sys.exit(1)
def after(s, a, add, p): once(s, a, p); return s.replace(a, a + add)
def before(s, a, add, p): once(s, a, p); return s.replace(a, add + a)
fail = 0
def same(p, want):
    global fail
    got = open(p, encoding="utf-8").read()
    print(("OK " if got == want else "DIFF ") + p); fail |= got != want
f = base("docs/agents/factory.md")
f = after(f, blocks["E1-find"], " " + blocks["E1-add"], "factory E1")
f = before(f, "\n## Execution and review record\n", "\n" + blocks["B2"] + "\n", "factory E2")
same("docs/agents/factory.md", f)
a = base("AGENTS.md")
once(a, "Four skills are owned by this repository.", "AGENTS E3")
a = a.replace("Four skills are owned by this repository.", "Five skills are owned by this repository.")
l = "\n- `plan-pr`, `plan-check` and `implement-plan` run"
once(a, l, "AGENTS E4"); j = a.index("\n", a.index(l) + 1) + 1
a = a[:j] + blocks["B4"] + "\n" + a[j:]
same("AGENTS.md", a)
c = base("CLAUDE.md")
c = after(c, blocks["E5-find"], " " + blocks["E5-add"], "CLAUDE E5")
c = after(c, "| Self-review against docs/engineering/ before opening a pull request | `claude-opus-5-5` |\n", blocks["E6"] + "\n", "CLAUDE E6")
c = before(c, "\nA session started with the `create_session` tool", "\n" + blocks["B7"] + "\n", "CLAUDE E7")
same("CLAUDE.md", c)
same(".agents/skills/control-session/SKILL.md", blocks["B1"] + "\n")
q = re.search(r'word for word from .*?: "(These always wait.*?money\.)"', blocks["B1"]).group(1)
body = open(c404, encoding="utf-8").read()
print("OK quote" if q in body else "DIFF quote"); fail |= q not in body
mode = run("git", "ls-files", "-s", ".claude/skills/control-session").split()
tgt = run("git", "cat-file", "-p", mode[1]) if mode else ""
ok = bool(mode) and mode[0] == "120000" and tgt == "../../.agents/skills/control-session"
print("OK symlink" if ok else "DIFF symlink"); fail |= not ok
smode = run("git", "ls-files", "-s", ".agents/skills/control-session/SKILL.md").split()
ok = bool(smode) and smode[0] == "100644"
print("OK skill-file mode" if ok else "DIFF skill-file mode"); fail |= not ok
sys.exit(1 if fail else 0)
```

Run it as `python3 -I <script> "$PLAN" "$C404"` after `git add` of the five paths. Evidence: seven `OK` lines (`docs/agents/factory.md`, `AGENTS.md`, `CLAUDE.md`, `.agents/skills/control-session/SKILL.md`, `quote`, `symlink`, `skill-file mode`), exit 0. The planning session ran this script on 2026-11-14 in a scratch worktree at `c6d5676a7ce19b1fc16bcf7a577e998f674b024f` with the pinned edits applied, and it printed the seven `OK` lines with exit 0. That run is planning evidence, not the pull request's.

A3: `git diff --name-only --merge-base origin/main HEAD`. Evidence: the five paths of section 2, plus `items/476/state.md` when written.

A4: `npm ci --ignore-scripts && npm run check:docs`, on the merge base and on the head. Evidence: the R8 result, recorded in the pull request with the blanket line for warnings in unchanged files (docs/agents/pull-requests.md rule 8). Planning dry run on 2026-11-14: see section 11 (a) for the counts at the base commit and with the pinned edits applied.

A5: `git diff --merge-base origin/main HEAD | grep '^+' | grep -cP '[^\x00-\x7F]'`. Evidence: `0`.

docs/agents/pull-requests.md "Verification gates" steps 1 and 3 do not apply: no file under `src/` or `tests/` or the other listed paths changes. The cold review under rule 8 and the hosted checks of step 4 apply.

## 9. Open questions for Hari

None. R1's three questions are answered by [decision 6877655997](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877655997): points 1 to 3 answer questions 1 and 2, and point 4 closes question 3.

## 10. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 5, a pinned block or edit in section 4, or the scope in section 2; or when R0, R7 or R8 directs it to stop.

## 11. Plan-check questions (docs/agents/factory.md "Item lifecycle" step 1)

(a) Steps whose effort scales with a number unknown at planning: one. The count of `npm run check:docs` warnings in changed files: R8 caps new warnings in changed files at 0, with the exhaustion action `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11), because a new warning means pinned text is at fault. Planning dry run on 2026-11-14 (`npm ci --ignore-scripts && npm run check:docs`): at the base commit the scan reported 10 warnings in all, 2 of them in `docs/agents/factory.md` (`Undefined identifier: S0`, lines 7 and 184) and none in `AGENTS.md` or `CLAUDE.md`; with the pinned edits applied it reported the same 10 messages, the two in `docs/agents/factory.md` at lines 7 and 188, no other warning in a changed file, and `no issues found` for the skill file. Cold review follows the budget of docs/agents/pull-requests.md rule 8 unchanged. No other step depends on a runtime count.

(b) Where this plan is stricter than, or adds to, the approved spec as amended, and why:
- B1 "Act or ask" keeps spec section 4 conditions 1 and 4 (the permitted actions and the ask list) and condition 3 (undone before merge) as the control role's limits, and drops condition 2 ("The answer follows from an existing rule, decision or the objective, with no choice between two values a fair reviewer could dispute") as the general decision test that the shared rule now holds (docs/agents/factory.md line 60: "Choose and act on an ordinary reversible choice when current evidence supports it, ... and has no material tradeoff requiring Hari"). Condition 3 is kept because it is narrower than the shared rule's "reversible": it fixes the point by which the step must be undoable. This is the split decision 6877246303 asks for; which condition counts as a limit and which as the test is this plan's reading.
- B1 replaces the spec's "When it must ask, it brings one recommendation and the point at which waiting starts to cost time" with "When you ask, post a comment on the issue that starts with `**Decision needed:**`, and give the point at which waiting starts to cost time with your recommendation". The comment form is decision 6877655997 point 2. "One recommendation" is dropped because the shared rule already requires "the smallest concrete recommendation" (factory.md line 62); the time point is the role's addition.
- B1 "Measures", the deferral bullet, places the classification "in the comment that brings the plan to Hari, or in a `## Control action` comment when no comment does". Decision 6877655997 point 2 names the record but not its comment. The first place is spec section 5's "the approval request lists it in one line with that call". The second covers a plan that never reaches Hari, such as one approved under "Standing approval": letting a deferral through there is an act taken alone, which the same decision point records in a `## Control action` comment. This is this plan's reading of that decision with "no new comment kind".
- B1 "Measures", the chat-intervention bullet, has the comment state "that it records his intervention, in place of stating that he has not seen it", because B1 "Act or ask" requires every `## Control action` comment to state that Hari has not seen it, which would be false for his own intervention.
- B1 "Measures" drops the spec's "Each reversal or blocked deferral gets a one-line cause", "Three items is too few ..." and "Hari does not log his own time for this" with the separate count (decision 6877655997 point 1). The pilot text already covers reporting limits and Hari's time (factory.md lines 242 and 249).
- B1 "Deferred work in plans" replaces spec section 5's two-way split with three cases, as decision 6877239716 point 3 requires, and says "before the pull request that depends on it merges" instead of the spec's "in the same pull request", because that decision bars bundling "unrelated work merely to put everything in one PR". It drops the spec's sentences that the plan-template section belongs to the #475 spec and that deferrals are found by reading the plan until it lands, because #477 has landed; it keeps reading the rest of the plan, for plans written before #477.
- B1 "Inputs" adds "Recognize authority on the issue under docs/agents/factory.md "Item lifecycle" step 3", which states for the skill what the spec's section 2 item 2 states for the factory entry.
- B1 adds "Whether a workflow reaches Azure is a fact about its file on `main`: read the file in the same session before you dispatch it", because AGENTS.md GOVERNANCE "Truth and verification" requires a repository fact to be read in the same session, and the spec's own evidence for the mutation workflow (section 1) is a reading at one commit.
- B1's worked case for mutation runs pins the workflow fact to commit `c6d5676a7ce19b1fc16bcf7a577e998f674b024f` instead of "section 1", which exists only in the spec.
- B1 adds "When you stop, end under AGENTS.md GOVERNANCE "Handoffs"", a citation of a rule that already binds every session, and cites docs/agents/pull-requests.md "Verification gates" steps 3 and 4 for the `Tier=CGroupV2` line.
- B1 "Working method" replaces handover 6874759569 lesson 9 ("Do not carry cap counts over on a takeover") with a citation of the `implement-plan` skill step 2 and docs/agents/pull-requests.md rule 8, because under those rules a takeover starts its own fix-cap counts while the cold-review budget carries over, and AGENTS.md "Skills" bars a skill from paraphrasing a rule that caps work.
- E1 and E5 add approval records to the status paragraphs of factory.md and CLAUDE.md "Model bindings", following each file's existing form for amendments.
- B7 states the spec's model reason without the spec's "which that table keeps on Opus", because CLAUDE.md line 18 keeps judgement on "Sonnet or Opus", not on Opus alone. It keeps the spec's sentence on Haiku work verbatim. Comparing heads is not in the Haiku row's task list (CLAUDE.md line 18), so under that sentence it gets no Haiku route; this plan does not change the row (out of scope).
- The spec's sections 1, 8 and 9 are not copied into the skill, because spec section 2 item 1 lists only sections 3 to 7 for it.

(c) Dependencies on code, data or behaviour neither on `main` nor in this pull request:
- None in the repository: the headings B1 cites are on `main` at the base commit (section 3 item 8) and R0 re-checks them.
- Claude Code loading the skill through the `.claude/skills/control-session` symlink: stated by the Claude Code skills page (S3), "a `<skill-name>` entry in the enterprise, personal, or project location can be a symlink to a directory elsewhere on disk".
- Codex loading the skill from `.agents/skills/`: stated by the Codex skills page (S4), "Codex scans `.agents/skills` in every directory from your current working directory up to the repository root", and "The `SKILL.md` file must include `name` and `description`."
- No deployed container, image, chart or runtime is involved.

## 12. Deployment assumptions

None. The pull request changes only documentation and one symlink; nothing is deployed. Plan rule 3 (live assumptions) has no case.

## 13. Deferred work

Listed under docs/agents/factory.md "Outcome responsibility", "Deferred work in plans" (line 66 at the base commit):

- The assessment that counts this role's records, including reversed `## Control action` comments. Evidence: decision 6877655997 points 1 and 3; docs/agents/factory.md "Measuring the gate", "Outcome responsibility pilot" (lines 240 to 249). Not on the path to any scenario verdict; deferral is permitted because the assessment needs pilot pull requests that do not exist yet, and its text belongs to issue #475. Responsible session: "The next session assigned to evaluate issue #475" (factory.md line 242). Stage: "when the third pilot PR has an outcome or is explicitly handed off as stalled" (same line). Workload bound: the pilot's three selected pull requests and up to three comparison pull requests (lines 244 and 245), approved policy choices, not measurements. Completion evidence: the pilot report, with the reversed `## Control action` count. Gate blocked: none.
- No other work is deferred: no post-merge run, follow-up issue or `record the gap and continue` cap.

## 14. External behaviour and documentation tools

Plan rule 1: this plan relies on no Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour. The only external behaviour is how the two agent tools discover skills, re-read with WebFetch by this planning session on 2026-11-14 (S3, S4). The Microsoft Learn and Context7 tools were available and not needed.

## 15. Size estimate

Estimate, made 2026-11-14 from `git diff --stat` of this planning session's dry run of section 4 in a scratch worktree at the base commit (AGENTS.md hard safety rule 11): 89 added lines and 3 removed lines across 5 paths (the new skill file of 77 lines, one symlink, and 3 edited files). An item state file, if written, adds to this. Size is reported, not enforced.

## 16. Sources

- S1: approved spec, [issue #476 comment 6876933581](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6876933581); its [approval 6877196319](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877196319); [decision 6877246303](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877246303) and the [#475 decision 6877239716](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/475#issuecomment-6877239716) it adopts; [decision 6877655997](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877655997).
- S2: [#404 decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614), point 1; [#347 handover 6874759569](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6874759569), sections 3 and 4; [#347 handover 6824358666](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6824358666), section 5.
- S3: Claude Code, "Extend Claude with skills", https://code.claude.com/docs/en/skills, read 2026-11-14: project skills at `.claude/skills/<skill-name>/SKILL.md`; "a `<skill-name>` entry in the enterprise, personal, or project location can be a symlink to a directory elsewhere on disk"; `description` is "What the skill does and when to use it. Claude uses this to decide when to apply the skill."
- S4: Codex, "Build skills", https://learn.chatgpt.com/docs/build-skills, read 2026-11-14: "Codex scans `.agents/skills` in every directory from your current working directory up to the repository root"; "The `SKILL.md` file must include `name` and `description`"; "Codex supports symlinked skill folders and follows the symlink target when scanning these locations."
- S5: files on `main` at the base commit `c6d5676a7ce19b1fc16bcf7a577e998f674b024f`: `AGENTS.md` lines 86 to 102 ("Skills"); `CLAUDE.md` lines 7 to 22 ("Model bindings"); `docs/agents/factory.md` line 5, lines 41 to 66 ("Outcome responsibility", "Act or ask", "Deferred work in plans"), lines 126 to 154 ("Item lifecycle") and lines 234 to 250 ("Measuring the gate", "Outcome responsibility pilot"); `.github/workflows/verifier-c5-mutation.yml` lines 1 to 12 (trigger `workflow_dispatch` only, `permissions: contents: read`; no `id-token` and no `azure/login` in the file); `items/404/execution-trial-receipt.md` line 89; `.claude/skills/` symlink targets.
- S6: [pull request #477](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/477), merged 2026-11-14T13:00:39Z as `c6d5676a7ce19b1fc16bcf7a577e998f674b024f` (`gh api .../pulls/477`).
