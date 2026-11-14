# control-session-skill contract-grade plan

Revision: R1. Issue: [#476](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476). Base commit: `5ac85af17f4b7dbc1a1396932fb3554d363752fb` (`main`, read 2026-11-14). Predecessor: pull request [#477](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/477), read at head `0563c2069fa8beacb69834f79f5dab2603258356`.

This plan adds a repository-owned `control-session` skill that gives the control session of a scenario issue its objective, its own limits on acting alone and a check for deferred work in plans, and points to it from docs/agents/factory.md, AGENTS.md and CLAUDE.md. The skill points to the shared act-or-ask rule and the deferred-work rule that pull request #477 adds to docs/agents/factory.md, as Hari's decision of 2026-11-14 on this issue requires, so this pull request is built after #477 merges. Every new sentence is pinned below, and three points the authorities leave open are under "Open questions for Hari".

## 1. Goal and authority

Goal: implement the approved spec of issue #476 (cause 1 of issue #475), as amended by Hari's coordination decision, as one documentation pull request. Verdict gates do not change.

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3):

1. The issue body, `updated_at` 2026-11-14T12:26:34Z.
2. The spec, [comment 6876933581](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6876933581), created and updated 2026-11-14T11:59:27Z. It is the item plan.
3. [`## Approval` 6877196319](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877196319), 2026-11-14T12:22:06Z: Hari approved the spec at `updated_at` 2026-11-14T11:59:27Z, "Tool: Claude Code". `ready-for-agent` was applied at 2026-11-14T12:21:57Z (issue events).
4. [`## Decision` 6877246303](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877246303), 2026-11-14T12:26:34Z, which links and adopts [#475 decision 6877239716](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/475#issuecomment-6877239716) (2026-11-14T12:25:58Z). For this issue it says: "Use the single shared act-or-ask rule in `docs/agents/factory.md` when it is available. Keep section 4's control-role permitted actions and restrictions as role-specific limits; do not duplicate the general decision test. Whichever coordinated change lands second replaces its copy with that pointer." It also says that issue #475 owns the plan `Deferred work` section and deferral policy, that the control session classifies each listed item against that policy, and that section 5 "must distinguish preventably deferred premerge preparation from execution that is authorized only after merge", without requiring "a final mutation-acceptance run before merge when existing rules require it on `main` after merge".

No other comment exists on the issue (catch-up and refresh on 2026-11-14, page 1 of `issues/476/comments?per_page=100`, three comments). There is no item state file (`items/476/` does not exist on `main`). There is no split approved on the issue and no earlier plan comment, so candidate selection gives the whole issue as one pull request, named `control-session-skill`. The work is one coherent piece under "Item lifecycle" step 3 "Split by scope": one new skill and the three short entries that name it, which cannot merge separately without dangling references.

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

Paths that must not change: every other path, including `skills-lock.json` (repository-owned skills are not in it, AGENTS.md line 95), `docs/agents/pull-requests.md`, the "Outcome responsibility" section of docs/agents/factory.md that #477 adds, every other file under `.agents/skills/` and `.claude/`, `.github/`, `results/` and every `expected.yaml`. The one exception is `items/476/state.md`, which the implementing session writes under docs/agents/factory.md "Item state file"; its content is that section's, not this plan's.

Out of scope (spec section 9 and decision 6877239716 point 4): any verdict gate, cap, review budget or merge rule; the shared act-or-ask rule and the deferred-work rule themselves (issue #475); Hari's personal instructions; the CLAUDE.md Haiku row's task list; lighter routes (issue #419); any script for the measures in B1 "Measures".

## 3. External contracts

These are the strings other sessions and later scripts read. They are pinned exactly.

1. Skill directory name and frontmatter `name`: `control-session`.
2. Skill frontmatter `description`: the line in block B1. Claude Code uses the description "to decide when to apply the skill"; Codex requires `name` and `description` (sources S3, S4).
3. Comment heading: `## Control action`. It carries no authority under docs/agents/factory.md "Item lifecycle" step 3.
4. factory.md section heading: `## Control session`. AGENTS.md and the skill cite it as docs/agents/factory.md "Control session".
5. CLAUDE.md "Model bindings" row: ``| Control session | `claude-opus-5-5` |``.
6. The ask-list quotation in block B1, which must equal byte for byte the substring of [#404 decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) point 1 that starts `These always wait for Hari` and ends `anything that spends money.` (that comment's `updated_at` 2026-11-09T21:52:11Z).
7. The headings B1 cites in docs/agents/factory.md after #477: `## Outcome responsibility`, and under it `### Act or ask` and `### Deferred work in plans` (#477 head `0563c2069fa8beacb69834f79f5dab2603258356`, factory.md lines 41, 58 and 64).

## 4. Pinned text

Each block is copied exactly, without its fence lines. Each edit is an exact replacement on the file at the merge base with `origin/main`; each `Find` string and anchor line occurs exactly once there. The anchors were checked on both the base commit and #477's head.

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

When you ask, give the point at which waiting starts to cost time with your recommendation.

Worked cases from issue #404:

| Case | Action |
|---|---|
| Plan defers timing of new mutation targets to after merge (#467) | Before approval: act, send the plan back. After approval: the timing is acceptance, so ask Hari the same day with "revise before merge". |
| Small sign-in fix through full process | Act on the route: use docs/agents/factory.md "Item lifecycle" step 3 "Standing approval" where its conditions hold. Ask on the timeout and retry values (ask list). Lighter routes are #419's. |
| Merge with an unresolved P1 (#455) | Act: post a not-ready note with head, checks and Codex state on each new head. It cannot stop Hari's merge. |
| Cold review looping on receipt wording | Ask: the cold-review budget is a gate. Ask after the first paperwork-only round, not when the budget is spent. |
| Mutation run failures (#469, #470) | Act: diagnose at the first failure, start the fix session, and dispatch the rerun on `main`. On `main` at 5ac85af17f4b7dbc1a1396932fb3554d363752fb, `.github/workflows/verifier-c5-mutation.yml` had no `azure/login` step and no `id-token` permission; read it again before each dispatch. |

## Deferred work in plans

Before a plan reaches Hari, read its `Deferred work` section and anything else the plan defers: to a post-merge run, a follow-up issue, a cap exhaustion action `record the gap and continue`, or anything known only after merge. Classify each item under docs/agents/factory.md "Outcome responsibility", including its "Deferred work in plans":

- Preparation on the path to the verdict that could be done before merge: send the plan back to resolve it before the pull request that depends on it merges, with a measured cost or a cap. Hari is not asked.
- Execution that existing rules allow only after merge, such as the mutation acceptance run on `main`: it stays at that stage, with its completion and publication gates. Do not ask for it before merge.
- Not on the path to the verdict: the item may stay deferred, and the approval request lists it in one line with that call.

This catches no item you cannot see.

## Measures

No targets are set and no speed-up will be claimed (AGENTS.md hard safety rule 11). After the next three items run with a control session (three is a choice, not a measurement), a script counts from GitHub:

1. `## Control action` comments Hari reversed.
2. Questions brought to Hari that were not on the ask list.
3. Deferred items marked "not on the path" that later blocked the path.
4. Hari's interventions per item: `## Decision` and `## Approval` comments recorded for him, plus chat interventions the session recorded.

Each reversal or blocked deferral gets a one-line cause. Three items is too few to show a speed-up, and items differ in difficulty; the review mainly shows whether the change does harm. Hari does not log his own time for this.

## Working method

From [#347 handover 6874759569](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6874759569) section 3, as method, not new gates:

- Post a comment, then link its ID from the tool result.
- Record only approvals Hari confirmed.
- Fix at the first failure.
- Check the head, every check and Codex state for the exact head before saying "ready".
- Recheck plans against merged code.
- Ask child sessions to use the documentation tools and cite.
- Do not carry cap counts over on a takeover.
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

Insert block B2, then one empty line, immediately before the line `## Execution and review record`. The empty line already before that heading stays, so B2 has one empty line on each side.

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
The pull request for issue #476 amends this section (the Control session row and the paragraph after the Haiku row); Hari's merge of that pull request is the approval of that amendment.
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

R0. WHEN the implementing session starts, THEN it checks that pull request #477 has merged and that `docs/agents/factory.md` on current `main` has the line `## Outcome responsibility` and, after it and before the next `## ` heading, the lines `### Act or ask` and `### Deferred work in plans`. IF #477 has not merged, THEN it hands off under AGENTS.md GOVERNANCE "Handoffs", naming #477 as the unmerged predecessor. IF #477 has merged but any of the three headings is missing or renamed, THEN it stops under section 10.

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

R11. WHEN Hari answers an open question in section 9, THEN a plan revision applies the answer as pinned text before implementation; the implementer adds no text for an open question on its own.

## 6. Invariants and safety rules

- No verdict gate, cap, review budget, merge rule or hard safety rule changes. The skill cites AGENTS.md "Before opening a pull request" rules 8 and 11 and GOVERNANCE "Merge rule" and does not paraphrase them (AGENTS.md "Skills", line 100).
- The skill holds no second copy of the general act-or-ask test or of the deferral policy; it cites docs/agents/factory.md "Outcome responsibility" for both and adds only the control role's limits (decision 6877246303).
- The skill grants no authority to post `## Approval` or to merge, and the `## Control action` heading carries no authority.
- No secret, key, tenant ID or subscription ID is added (hard safety rule 7). No workflow file changes (hard safety rule 8).
- No number is stated that was not measured, except the spec's choice of three items, which B1 labels a choice (hard safety rule 11).
- `skills-lock.json` is unchanged; the new skill is repository-owned.
- Plan rule 2 (standing fixture rule) does not apply: this plan adds and tightens no check and changes no test.

## 7. Test map

The pull request changes no code and no test; its checks are the acceptance commands of section 8. Each requirement maps to one check, with its literal expected result.

| Req | Check | Input | Expected |
|---|---|---|---|
| R0 | A0 `predecessor` | `gh api .../pulls/477 --jq .merged`; `git show origin/main:docs/agents/factory.md` | `true`; `OK headings` |
| R1 | A1 `skill-file` | branch file vs B1; `git ls-files -s` | `OK .agents/skills/control-session/SKILL.md`; `OK skill-file mode` (`100644`) |
| R2 | A1 `symlink` | `git ls-files -s .claude/skills/control-session` and `git cat-file -p` of its object | `OK symlink`: mode `120000`, target `../../.agents/skills/control-session` (36 bytes, no newline) |
| R3 | A1 `factory.md` | merge-base text with E1, E2 vs branch | `OK docs/agents/factory.md` |
| R4 | A1 `AGENTS.md` | merge-base text with E3, E4 vs branch | `OK AGENTS.md` |
| R5 | A1 `CLAUDE.md` | merge-base text with E5, E6, E7 vs branch | `OK CLAUDE.md` |
| R6 | A3 `paths` | `git diff --name-only --merge-base origin/main HEAD` | exactly the five paths, plus `items/476/state.md` if written |
| R7 | A1 anchor counts | each `Find` string and anchor line in the merge-base file | count 1 each; any other count makes A1 print `ANCHOR` and exit 1 |
| R8 | A4 `check:docs` | `npm run check:docs` on the merge base and on the head | skill file: `no issues found`; the three edited files: the same messages on both |
| R9 | A5 `ascii` | added lines of the diff | `0` |
| R10 | A1 `quote` | B1 quote vs comment 6752430614 body | `OK quote` |

Boundaries: A1 fails on a missing or extra trailing newline in the skill file (exact bytes), on an anchor that occurs zero or two times, and on any extra changed line in the three edited files.

## 8. Acceptance commands

Run from the repository root on the pull request branch, with this plan saved outside the repository at `$PLAN`. Nothing these commands write is committed (docs/agents/pull-requests.md rule 8).

A0: `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/pulls/477 --jq .merged` prints `true`, and then:

```python
import subprocess, sys
f = subprocess.run(["git", "show", "origin/main:docs/agents/factory.md"], capture_output=True, text=True, check=True).stdout.split("\n")
i = f.index("## Outcome responsibility")
j = next((k for k in range(i + 1, len(f)) if f[k].startswith("## ")), len(f))
ok = "### Act or ask" in f[i:j] and "### Deferred work in plans" in f[i:j]
print("OK headings" if ok else "MISSING headings"); sys.exit(0 if ok else 1)
```

Evidence: `true`, then `OK headings`, exit 0. `ValueError` from `index` means `## Outcome responsibility` is missing (R0).

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

Run it as `python3 -I <script> "$PLAN" "$C404"` after `git add` of the five paths. Evidence: seven `OK` lines (`docs/agents/factory.md`, `AGENTS.md`, `CLAUDE.md`, `.agents/skills/control-session/SKILL.md`, `quote`, `symlink`, `skill-file mode`), exit 0. The planning session ran this script on 2026-11-14 in a scratch worktree with the pinned edits applied to #477's head `0563c2069fa8beacb69834f79f5dab2603258356`, standing in for `origin/main`, and it printed the seven `OK` lines with exit 0. That run is planning evidence, not the pull request's.

A3: `git diff --name-only --merge-base origin/main HEAD`. Evidence: the five paths of section 2, plus `items/476/state.md` when written.

A4: `npm ci --ignore-scripts && npm run check:docs`, on the merge base and on the head. Evidence: the R8 result, recorded in the pull request with the blanket line for warnings in unchanged files (docs/agents/pull-requests.md rule 8). At the base commit the scan reported 10 warnings in all: 2 in `docs/agents/factory.md` (`Undefined identifier: S0`, lines 7 and 153) and none in `AGENTS.md` or `CLAUDE.md`. On #477's head `0563c2069fa8beacb69834f79f5dab2603258356` the same two messages appear in `docs/agents/factory.md` (lines 7 and 184), and with the pinned edits applied they appear at lines 7 and 188, with no other warning in a changed file and `no issues found` for the skill file (all runs by this planning session on 2026-11-14).

A5: `git diff --merge-base origin/main HEAD | grep '^+' | grep -cP '[^\x00-\x7F]'`. Evidence: `0`.

docs/agents/pull-requests.md "Verification gates" steps 1 and 3 do not apply: no file under `src/` or `tests/` or the other listed paths changes. The cold review under rule 8 and the hosted checks of step 4 apply.

## 9. Open questions for Hari

1. **Where the measures are recorded.** B1 "Measures" (spec section 6) has a script count from GitHub questions brought to Hari that were not on the ask list, deferred items marked "not on the path", and chat interventions the session recorded. The spec gives these no place on GitHub: questions are asked in chat, the "not on the path" line goes into an approval request, and under "Standing approval" there is no approval request. Without a recorded form, counts 2 to 4 cannot be taken from GitHub. Recommendation: add to B1 "Measures", after its list, "Record each question you bring to Hari, each deferred item you mark not on the path, and each intervention Hari makes in chat, as one comment on the issue headed `## Control record`, whose first line is `Kind: question`, `Kind: deferral` or `Kind: intervention`; a question also names the ask-list entry it falls under, or states `not on the ask list`. That heading carries no authority under docs/agents/factory.md "Item lifecycle" step 3." Waiting costs nothing until the first control session runs under the merged skill; after that, events not recorded cannot be counted.
2. **Who runs the count, and which items count.** The spec says "after the next three items run with a control session" and names no session, no start and no end. Recommendation: count the first three scenario issues whose control session starts after this pull request merges, in order of their first `## Control action` comment, and have a session Hari starts on issue #476 run the count once the third item's verdict pull request has merged or closed. If Hari prefers to decide this later, the skill keeps the spec's wording and the pull request does not wait for it.
3. **Merge order.** Decision 6877246303 says to use the shared rule "when it is available" and that "whichever coordinated change lands second replaces its copy with that pointer". This plan has this pull request land second (R0), because #475 decision 6877239716 point 4 says the #477 change "does not add or edit that skill", so if this pull request landed first, no planned change would remove its copy of the test. Recommendation: confirm that this pull request waits for #477. The alternative is a revision that pins a full copy of spec sections 4 and 5 for the case that this pull request lands first, plus a decision naming the change that later replaces that copy with the pointer.

## 10. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 5, a pinned block or edit in section 4, or the scope in section 2; when R0, R7 or R8 directs it to stop; or when an open question in section 9 has no answer and affects the pull request. Open questions 1 and 3 affect it; open question 2 affects it only if Hari's answer adds text.

## 11. Plan-check questions (docs/agents/factory.md "Item lifecycle" step 1)

(a) Steps whose effort scales with a number unknown at planning: one. The count of `npm run check:docs` warnings in changed files: R8 caps new warnings in changed files at 0, with the exhaustion action `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11), because a new warning means pinned text is at fault. The planning dry run on #477's head found none. Cold review follows the budget of docs/agents/pull-requests.md rule 8 unchanged. No other step depends on a runtime count.

(b) Where this plan is stricter than, or adds to, the approved spec as amended, and why:
- R0 makes #477 a predecessor; open question 3 asks Hari to confirm it.
- B1 "Act or ask" keeps spec section 4 conditions 1 and 4 (the permitted actions and the ask list) and condition 3 (undone before merge) as the control role's limits, and drops condition 2 ("The answer follows from an existing rule, decision or the objective, with no choice between two values a fair reviewer could dispute") as the general decision test that the shared rule now holds ("Choose and act on an ordinary reversible choice when current evidence supports it, ... and has no material tradeoff requiring Hari"). Condition 3 is kept because it is narrower than the shared rule's "reversible": it fixes the point by which the step must be undoable. This is the split decision 6877246303 asks for; which condition counts as a limit and which as the test is this plan's reading.
- B1 replaces the spec's "When it must ask, it brings one recommendation and the point at which waiting starts to cost time" with "When you ask, give the point at which waiting starts to cost time with your recommendation", because the shared rule already requires "the smallest concrete recommendation"; the time point is the role's addition.
- B1 "Deferred work in plans" replaces spec section 5's two-way split with three cases, as decision 6877239716 point 3 requires, and says "before the pull request that depends on it merges" instead of the spec's "in the same pull request", because that decision bars bundling "unrelated work merely to put everything in one PR". It drops the spec's sentences that the plan-template section belongs to the #475 spec and "Until it lands, find deferrals by reading the plan", because R0 makes it land first; it keeps reading the rest of the plan, for plans written before #477.
- B1 "Inputs" adds "Recognize authority on the issue under docs/agents/factory.md "Item lifecycle" step 3", which states for the skill what the spec's section 2 item 2 states for the factory entry.
- B1 adds "Whether a workflow reaches Azure is a fact about its file on `main`: read the file in the same session before you dispatch it", because AGENTS.md GOVERNANCE "Truth and verification" requires a repository fact to be read in the same session, and the spec's own evidence for the mutation workflow (section 1) is a reading at one commit.
- B1's worked case for mutation runs pins the workflow fact to commit `5ac85af17f4b7dbc1a1396932fb3554d363752fb` instead of "section 1", which exists only in the spec.
- B1 adds "When you stop, end under AGENTS.md GOVERNANCE "Handoffs"", a citation of a rule that already binds every session, and cites docs/agents/pull-requests.md "Verification gates" steps 3 and 4 for the `Tier=CGroupV2` line.
- E1 and E5 add approval records to the status paragraphs of factory.md and CLAUDE.md "Model bindings", following each file's existing form for amendments.
- B7 states the spec's model reason without the spec's "which that table keeps on Opus", because CLAUDE.md line 18 keeps judgement on "Sonnet or Opus", not on Opus alone. It keeps the spec's sentence on Haiku work verbatim. Comparing heads is not in the Haiku row's task list (CLAUDE.md line 18), so under that sentence it gets no Haiku route; this plan does not change the row (out of scope).
- The spec's sections 1, 8 and 9 are not copied into the skill, because spec section 2 item 1 lists only sections 3 to 7 for it.

(c) Dependencies on code, data or behaviour neither on `main` nor in this pull request:
- Pull request #477 and its headings `## Outcome responsibility`, `### Act or ask` and `### Deferred work in plans`: checked against its source at head `0563c2069fa8beacb69834f79f5dab2603258356` (factory.md lines 41, 58 and 64), and gated by R0 and A0, since #477 may still change before it merges.
- Claude Code loading the skill through the `.claude/skills/control-session` symlink: stated by the Claude Code skills page (S3), "a `<skill-name>` entry in the enterprise, personal, or project location can be a symlink to a directory elsewhere on disk".
- Codex loading the skill from `.agents/skills/`: stated by the Codex skills page (S4), "Codex scans `.agents/skills` in every directory from your current working directory up to the repository root", and "The `SKILL.md` file must include `name` and `description`."
- No deployed container, image, chart or runtime is involved.

## 12. Deployment assumptions

None. The pull request changes only documentation and one symlink; nothing is deployed. Plan rule 3 (live assumptions) has no case.

## 13. Deferred work

Listed under #477's docs/agents/factory.md "Deferred work in plans" (head `0563c2069fa8beacb69834f79f5dab2603258356`, line 66):

- The measures count of B1 "Measures". Evidence: spec section 6. Not on the path to any scenario verdict; deferral is permitted because the count needs three items run under the merged skill. Responsible session: open question 2. Stage: after three items. Workload bound: three items, a choice in the spec, not a measurement. Completion evidence: the four counts with a one-line cause for each reversal or blocked deferral. Gate blocked: none.
- No other work is deferred: no post-merge run, follow-up issue or `record the gap and continue` cap.

## 14. External behaviour and documentation tools

Plan rule 1: this plan relies on no Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour. The only external behaviour is how the two agent tools discover skills, read with WebFetch on 2026-11-14 (S3, S4). The Microsoft Learn and Context7 tools were available and not needed.

## 15. Size estimate

Estimate, made 2026-11-14 from `git diff --stat` of the planning session's dry run of section 4 in a scratch worktree (AGENTS.md hard safety rule 11): about 89 added lines and 3 removed lines across 5 paths (the new skill file of about 75 lines, one symlink, and 3 edited files). An item state file, if written, adds to this. Size is reported, not enforced.

## 16. Sources

- S1: approved spec, [issue #476 comment 6876933581](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6876933581); its [approval 6877196319](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877196319); [decision 6877246303](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/476#issuecomment-6877246303) and the [#475 decision 6877239716](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/475#issuecomment-6877239716) it adopts.
- S2: [#404 decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614), point 1; [#347 handover 6874759569](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6874759569), sections 3 and 4; [#347 handover 6824358666](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6824358666), section 5.
- S3: Claude Code, "Extend Claude with skills", https://code.claude.com/docs/en/skills, read 2026-11-14: project skills at `.claude/skills/<skill-name>/SKILL.md`; "a `<skill-name>` entry in the enterprise, personal, or project location can be a symlink to a directory elsewhere on disk"; `description` is "What the skill does and when to use it. Claude uses this to decide when to apply the skill."
- S4: Codex, "Build skills", https://learn.chatgpt.com/docs/build-skills (redirected from https://developers.openai.com/codex/skills), read 2026-11-14: "Codex scans `.agents/skills` in every directory from your current working directory up to the repository root"; "The `SKILL.md` file must include `name` and `description`"; Codex "follows the symlink target when scanning these locations".
- S5: files on `main` at the base commit: `AGENTS.md` lines 86 to 102 ("Skills"); `CLAUDE.md` lines 7 to 22 ("Model bindings"); `docs/agents/factory.md` line 5 and lines 99 to 125 ("Item lifecycle"); `.github/workflows/verifier-c5-mutation.yml` lines 1 to 7 (trigger `workflow_dispatch` only, `permissions: contents: read`; no `id-token` and no `azure/login` in the file); `items/404/execution-trial-receipt.md` line 89; `.claude/skills/` symlink targets.
- S6: [pull request #477](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/477) at head `0563c2069fa8beacb69834f79f5dab2603258356`, read 2026-11-14: `docs/agents/factory.md` lines 41 to 66 ("Outcome responsibility", "Act or ask", "Deferred work in plans") and its changed-file list.
