# AGENTS.md split: receipt

This receipt records the third governance change of issue #264. AGENTS.md keeps a core that every session needs, and the rules for work that a pull request publishes move, unchanged, to docs/agents/pull-requests.md. A script maps every block of the old AGENTS.md to its new place and checks that every citation of an AGENTS.md section or rule still resolves.

## Execution card

- Deliverable: a shorter AGENTS.md, a new role file docs/agents/pull-requests.md, rule 13 links to the measurement decisions in force, pointers in CLAUDE.md and four repository-owned skills, and the check script `agents-split-check.py` beside this receipt.
- Authorization: [decision 6570202237](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6570202237), points 1 to 5: "one governance pull request linked to this issue, merged by Hari". The session prompt: "Implement the third governance change of issue #264, defined in decision 6570202237". Directly authorized governance work without an approved item plan; no plan approval is claimed. The decision's "Not decided here" (file layout, which rules stay in the core) is proposed below for Hari to decide at merge.
- Mandatory gates: `git diff --check origin/main`, `npm run check:docs`, the check script, the cold review under AGENTS.md "Before opening a pull request" rule 8 (now held in docs/agents/pull-requests.md), CI, hosted Codex review, and Hari's merge, because seven of the nine changed files are protected paths (`/*.md`, `/docs/`, `/.*/` in `.github/CODEOWNERS`). The script and this receipt are under `docs/operations/`, which agents own (docs/agents/factory.md "Ruleset").
- Verification commands: `git diff --check origin/main`; `npm run check:docs`; `python3 -I docs/operations/evidence/issue-264/agents-split-check.py origin/main`.
- Publication checkpoint: open the pull request as a draft after the first cold-review round with no P0 or P1.
- Review budget: rule 8 as written, no rounds used before this pull request. Exhaustion action: rule 8's review-budget handoff to Hari.
- Predicate-to-test map: not applicable; no verifier work. The check script is evidence tooling, and its negative test is recorded under "Verification".

## Proposal for Hari: layout and core

One role file, docs/agents/pull-requests.md. It holds the execution card, the predicate map and rules 1 to 10 of "Before opening a pull request", under their old numbers. Everything else stays in AGENTS.md. AGENTS.md keeps the "Before opening a pull request" heading with a pointer and rules 11 to 13, so every existing citation, including the 168 citations of rules 1 to 10 found by the script, resolves without editing the 245 tracked files outside `results/` that mention AGENTS.md on `dfc6cb1` (`git grep -l 'AGENTS.md' -- ':!results' | wc -l`).

What stays in the core, and why:

- GOVERNANCE, "Implementer and reviewer", "Skills", "GitHub issues and pull requests" and "Writing standards" bind every session: safety, merge, handoffs, commit trailers, the `ready-for-agent` ban, REST paging and punctuation.
- "Engineering standards" binds every code change, and its body is 692 characters (by script: the text between its heading and the next heading, stripped of surrounding whitespace), so moving it saves little.
- "Code Review Rules" stays because Codex code review reads the `## Code Review Rules` section of AGENTS.md files (source 4). The page does not say Codex review follows a link to another file.
- Rules 11 to 13 stay because they bind sessions that open no pull request. Rule 11 names planning and plan checking in its exemptions; rule 13 holds the cost-complaint stop and the measurement decisions.

Who reads the role file, and the pointer that sends them:

| Reader | Pointer |
|---|---|
| Any session whose work a pull request publishes | AGENTS.md opening paragraph and "Before opening a pull request" |
| Implementing session | `implement-plan` skill, opening line |
| Claude Code session opening a pull request | CLAUDE.md "Before opening a pull request" |
| Cold reviewer | `repo-review` rubric, "anything" row |
| Planning sessions, rules 1, 2 and 4 only | `plan-pr` step 3, and the AGENTS.md pointer; docs/agents/factory.md "Item lifecycle" step 1 already cites the rules |
| Plan-check sessions | `plan-check` step 3 says where those rules are now, without adding a check |

Alternatives not taken:

- Move all of "Before opening a pull request", rules 11 to 13 included. Planning and plan-check sessions would lose rules they are bound by.
- Import the role file from CLAUDE.md with `@`. Imports load with the importing file, so the context cost does not fall (source 1).
- Nested AGENTS.md or CLAUDE.md files. Both tools load them by directory: Codex from the project root down to the working directory (source 3), Claude Code once it touches a file in that subdirectory (source 1). A role is not a directory, so the load would not follow the role.
- `.claude/rules/` path-scoped rules. Claude Code only, and scoped by file path (source 1).

## Loading behaviour: verified and unverified

Read on 2026-11-02 at the URLs in "Claims and sources".

Verified:

- Claude Code reads CLAUDE.md here, and AGENTS.md through its `@AGENTS.md` import. With a CLAUDE.md present, Claude Code reads "Your `CLAUDE.md` files only", and a CLAUDE.md that imports AGENTS.md loads it "through the import" (source 1, "AGENTS.md").
- An `@` import does not cut context cost: imports "don't reduce its context cost, because imported files load together with the CLAUDE.md that imports them" (source 1, "Write effective instructions").
- A written pointer is read only if the agent opens the file. For a CLAUDE.md that tells Claude in words to read AGENTS.md: "Claude sees `AGENTS.md` only if it decides to open the file" (source 1, "Remove an earlier AGENTS.md workaround").
- Codex concatenates AGENTS.md files from the project root down to the working directory and "stops adding files once the combined size reaches the limit defined by `project_doc_max_bytes` (32 KiB by default)" (source 3).
- Codex code review "searches your repository for `AGENTS.md` files and follows the applicable code review rules", from a `## Code Review Rules` section (source 4).

Unverified, so the design does not depend on them:

- Whether Codex, in a session or in code review, follows a link inside AGENTS.md to another file. Neither Codex page says (sources 3 and 4). Inference: like Claude Code, a Codex session reads the role file only when it decides to, on the strength of the pointer. That is the mechanism every existing pointer in AGENTS.md already relies on, such as the one to docs/agents/factory.md. Its reliability is not measured.

Not established: whether the size limit was close. AGENTS.md on `dfc6cb1` was 29,370 bytes, 89.6% of 32,768 bytes, and the split leaves 19,707 bytes, 60.1% (by script). Source 3 calls the limit a combined size. The Codex configuration page (source 7) describes `project_doc_max_bytes` as "how much to read from each `AGENTS.md` file", which reads as per file. Neither page says whether a global `~/.codex/AGENTS.md` counts toward the limit. The design does not depend on this.

## Guardrail map

`agents-split-check.py` against `origin/main` at `dfc6cb1` reports 117 blocks of the old AGENTS.md: 93 kept in AGENTS.md, 22 moved verbatim to docs/agents/pull-requests.md, and 2 changed. The script prints each changed block's word diff, and both diffs add words and remove none, except that the end of rule 13's decision-3 sentence changes "3." to "3,":

1. The opening paragraph adds the pointer sentence to docs/agents/pull-requests.md.
2. Rule 13 adds links to [decision 6555901288](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6555901288) (plan-check replays only) and [decision 6561538906](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6561538906) (the before-and-after method), as decision 6570202237 point 3 requires, and to decision 6570202237 point 5.

The moved text changes in one place only: the link to docs/engineering/documentation-checks.md in rule 5 becomes `../engineering/documentation-checks.md`, because the file moved; the script resolves relative links before comparing. The #263 losses (rule 5's checker-capability sentence, citations of hard safety rules 6 and 10) are each a block the script would report as changed or gone. The negative test under "Verification" shows it does.

The new text is the pointer paragraphs in AGENTS.md "Before opening a pull request", including a sentence that extends AGENTS.md's "this file wins" precedence (its opening bullets and "Skills") to docs/agents/pull-requests.md, the opening three paragraphs of docs/agents/pull-requests.md, and one sentence each in CLAUDE.md and the `implement-plan`, `plan-pr`, `plan-check` and `repo-review` skills. Each one adds a reader or a pointer; none removes a rule or adds a duty.

## Claims and sources

1. Claude Code memory and imports: https://code.claude.com/docs/en/memory, sections "Write effective instructions", "Import additional files", "How CLAUDE.md files load", "AGENTS.md", "Remove an earlier AGENTS.md workaround", read 2026-11-02. Claude Code version in this session: 2.1.316 (`claude --version`).
2. This repository's import: CLAUDE.md line 1, `@AGENTS.md`.
3. Codex AGENTS.md discovery and size limit: https://learn.chatgpt.com/docs/agent-configuration/agents-md, read 2026-11-02 (https://developers.openai.com/codex/guides/agents-md redirects there with status 308).
4. Codex code review and AGENTS.md: https://learn.chatgpt.com/docs/third-party/github, section "Customize what Codex reviews", read 2026-11-02 (https://developers.openai.com/codex/integrations/github redirects there with status 308).
5. The decision's figures for `dfc6cb1` (29,370 characters, 4,430 words; CLAUDE.md 6,606 characters) match the script output below.
6. Rule 11 binds planning and plan checking: its sentence "Required authority reads for planning, plan checking or implementation are exempt", AGENTS.md on `dfc6cb1`.
7. Codex configuration: https://learn.chatgpt.com/codex/config-file/config-advanced, section "Project instructions discovery", read 2026-11-02.

## Verification

| Command | Observed result |
| --- | --- |
| `python3 -I docs/operations/evidence/issue-264/agents-split-check.py origin/main` | Exit 0. Map totals: CHANGED 2, KEPT 93, MOVED 22. Citations checked: 437; failures: 0; resolved through the pointer: 168; pre-existing unresolved: 1 (`hard safety rule 4` in docs/operations/evidence/issue-114/cold-review-3.md; AGENTS.md has no rule 4 on `dfc6cb1` either). |
| Negative test: delete rule 5's sentence "A new check needs explicit supported inputs, failure conditions and regression examples.", and break rule 6's number, then run the script; restore both | Exit 1. Rule 5 reported CHANGED with the deleted words printed; rule 6 reported GONE; factory.md's citation of rule 6 reported UNRESOLVED. The exit status comes from the GONE block and the citation: a CHANGED block alone does not fail, so its printed word diff is read by hand. After restoring, exit 0. |
| Sizes, by script | AGENTS.md 29,370 to 19,707 characters (32.9% fewer) and 4,430 to 2,881 words (35.0% fewer). docs/agents/pull-requests.md 12,450 characters. CLAUDE.md 6,606 to 6,688 characters. A session that reads both files reads 32,157 characters, 2,787 (9.5%) more than the old AGENTS.md. Measured on `866a1eb`; later commits do not change these files. |
| `git diff --check origin/main` | Exit 0, no output. Through round 2 the receipt recorded a bare `git diff --check`, which checks only unstaged changes; round 2 found a blank line at the end of docs/agents/pull-requests.md, now removed. |
| `npm run check:docs` | 8 warnings, the same count as on `dfc6cb1`, all in files this change does not touch: docs/agents/factory.md (2), and evidence files under issues 124, 140 and 211 (6). |

Source commit: `dfc6cb1`.

## Measurement after merge

Decision 6570202237 point 5: after the merge, a before-and-after Codex plan-check replay on the next real item, with Claude token totals from every session of that item. This pull request sets up nothing for it.

## Cold review

Comprehensive round 1: input is the staged diff against `origin/main` at `dfc6cb1`. Report and dispositions are in the pull request's self-review record.

Comprehensive round 2, the consolidated re-review: input is the branch head `94871ac` against `origin/main` at `dfc6cb1`, which is round 1's reviewed content (commit `98c1a70`) plus the fixes for its three P1 and two P2 findings, all fixed. The P1s were receipt evidence: citation counts that included this receipt's own quotes, a protected-path claim that missed `docs/operations/`, and a size figure with no stated definition. The P2s were a `plan-check` edit that added a duty, and a negative-test result that read as if a CHANGED block fails the script. Cumulative: 2 comprehensive, 0 focused.

Focused round 1: input is the branch head after the fixes for round 2's three P1 and one P2 findings, against `origin/main` at `dfc6cb1`; comparison revision `94871ac`. The P1s: rules 1 to 10 lost AGENTS.md's "this file wins" precedence; the recorded `git diff --check` did not cover committed work and missed a blank line at the end of the role file; and a claim that the global Codex file counts toward the size limit was not stated by its source. The P2: round 2's input was described as a staged diff. All fixed. Cumulative: 2 comprehensive, 1 focused.

Focused round 2, the last round the budget allows: input is the branch head after the fixes for focused round 1's one P1 (size figures measured before round 2's fixes, so stale) and one P2 (a bare `git diff --check` left in "Mandatory gates"), both fixed; comparison revision `866a1eb`, against `origin/main` at `dfc6cb1`. Cumulative: 2 comprehensive, 2 focused.
