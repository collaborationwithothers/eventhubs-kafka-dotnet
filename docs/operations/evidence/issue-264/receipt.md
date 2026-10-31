# Item state file and reading lists: receipt

This receipt records the first governance change from the factory-cost redesign. Sessions now read a per-item state file plus the comments newer than it, and a defined reading list per step, instead of the whole issue thread. Plan checks must also read each changed file at the place it changes.

## Execution card

- Deliverable: parts a, b and c of the "First governance change" in [Hari's decision of 2026-10-31](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6544062669): a state file per item, a reading list per step replacing "read everything" in docs/agents/factory.md "Authority reads", and source reading in plan checks. Its "Unchanged" list stays unchanged.
- Authorization: that decision comment, and its "Next session prompt": "Implement the first governance change defined on issue #264 as a pull request." Directly authorized governance work without an approved item plan; no plan approval is claimed.
- Later decisions, from Hari in the interactive Claude Code session that implemented this change, on 2026-10-31. The decision's example path `docs/items/245/state.md` is under `/docs/`, a protected path, so every update would need Hari's merge, and planning and plan-check sessions may not commit. Hari answered two questions:
  1. Where the state lives: "Unprotected path on main". Implemented as `items/<issue number>/state.md`. No pattern in factory.md "Ruleset" or `.github/CODEOWNERS` matches `items/`.
  2. Who commits updates: "Implementer only". The implementing session writes the file inside its implementation pull request. Planning and plan-check sessions stay read-only.
- Mandatory gates: `git diff --check`, `npm run check:docs`, one fresh-context cold review under AGENTS.md "Before opening a pull request" rule 8, CI, hosted Codex review, and Hari's merge, because AGENTS.md, docs/agents/ and `.agents/` are protected paths.
- Publication checkpoint: open a draft pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: rule 8's review-budget handoff to Hari.
- Predicate-to-test map: not applicable; no verifier work.

## Changes, by decision part

| Part | Where |
|---|---|
| a. State file per item | factory.md new section "Item state file"; "Item lifecycle" step 3 implementing bullet; `implement-plan` step 2 |
| b. Reading list per step | factory.md "Authority reads" rewritten; `plan-pr`, `plan-check` and `implement-plan` step 1; AGENTS.md "Before opening a pull request" rule 11 exemption sentence |
| c. Source reading in plan checks | factory.md "Item lifecycle" step 1; `plan-check` step 2 |
| Amendment record | factory.md "Status" line |

Choices made in this change that the decision did not state. Each is open to Hari at merge.

1. Staleness also checks edits. The decision names only "the ID of the newest issue comment the file covers". Plan revisions edit the plan comment in place (factory.md "Item lifecycle" step 3), so an ID check alone would miss them. The file therefore also records a read time, and a comment edited after it counts as new.
2. The plan check reads no new comment body except decisions, approvals, the decisions they link and adopt, the selected plan, and the approved item plan or parent plan (choice 7). This follows the decision's "Not the thread, not the author's handoffs". A script may extract plan and plan-check headings, links, revision labels and timestamps for candidate selection without the bodies entering context. Planning and implementation read every new comment body when a state file exists, because a handoff to the next session (AGENTS.md rule 11) carries the continuation.
3. With no state file, every comment is new, but no step reads the whole thread. Each reads the issue body, every decision and approval comment and the decisions they adopt, and the comments its row names. Planning and implementation also read the newest comment that is not a decision, approval, plan or plan check, to get the latest handoff. Under Hari's "implementer only" answer, a state file first reaches `main` when an item's first implementation pull request merges, so this fallback is the normal path for an item's first planning, check and implementation sessions, and for every single-pull-request child issue. The decision's table lists no thread reading for any step, so the fallback follows it.
4. "Decisions in force" starts with the issue body, because "Item lifecycle" step 3 says authority starts there.
5. A child issue reads and refreshes its listed parent items' bodies and their decision and approval comments, not their other comments. "Item lifecycle" step 3 uses the parent items for item approval and split checks, and a parent decision changes a child only when a child comment adopts it.
6. Each step also reads the AGENTS.md and factory.md sections its skill cites. The decision's table lists neither file; skills cite factory.md sections for selection and authority rules.
7. The state file adds "Item plan" and "Implementing tool" fields, and the reading lists name the approved item plan or parent plan and the tool-naming comment. Neither is a decision or approval comment, yet the plan-check questions in "Item lifecycle" step 1 and the tool check in step 2 depend on them.
8. "Use subagents at high effort for reading" is kept in `plan-pr` and `plan-check`. The results' decision rule 5 (subagents dominating tokens) held for runs 1 and 4, but the first change does not include it.

## Verification

| Command or inspection | Observed result |
| --- | --- |
| `git diff --check` | Exit 0, no output. |
| `npm run check:docs`, on source commit and on the change | 8 warnings in both, the same 8. Two are in docs/agents/factory.md, `Undefined identifier: S0`, in the #211 amendment sentence and the "Evidence pack" bullet; this change does not touch either text. The other six are in files this change does not touch. |
| Word count, Python `str.split()`, source commit to the commit after `6ac82a1` | AGENTS.md 4375 to 4377. docs/agents/factory.md 5693 to 6731. |
| `grep -rn -i "every comment\|full thread\|whole thread\|comment page"` over Markdown outside docs/operations/evidence | Before the change, one match: the old "Authority reads" first-read sentence. After, one match: the catch-up's "every comment's ID", which lists metadata only. |
| Ruleset patterns in factory.md "Ruleset" and `.github/CODEOWNERS` | No pattern matches `items/`. |

Source commit: `b79acb256d09ea29e493acc460ebbe2c9c59aa4f`.

## Cold review

Rounds, modes and dispositions are in the pull request's self-review record.
