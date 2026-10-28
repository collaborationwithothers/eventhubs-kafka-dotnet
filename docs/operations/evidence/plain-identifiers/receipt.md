# Plain-identifier rule validation receipt

This pull request adds one rule to AGENTS.md "Writing standards". Agents describe repository identifiers in plain words when talking to Hari in an interactive session. This receipt records the local checks; no program output is committed.

Source commit: `db7db93c4622f091d4abc31f93739d89320bbc47` (`main`).

Authority: Hari's instruction in an interactive Claude Code session on 2026-10-28. No issue or plan exists for this change, so the pull request has no plan checks.

| Command or inspection | Observed result |
|---|---|
| `git diff --check` | No whitespace diagnostics. |
| Python count of characters above code point 127 in AGENTS.md | 0. |
| `npm run check:docs` | Completed with the 8 existing warnings: factory.md (2), issue-124 README (1), issue-140 README (2) and issue-211 receipt (3). None is in a file this pull request changes. |
| `git diff --cached --name-only` | Exactly AGENTS.md and this receipt. |

## Cold review

Round 1: `Findings: none at P0 or P1`, with two P2s on the new rule. Both were fixed as prose in AGENTS.md under "Before opening a pull request" rule 8:
- The basis now states that Hari's merge records his approval, because the session instruction cannot be linked.
- The rule now says it covers chat replies only, not text posted to GitHub, and gives an example of each identifier kind that had no definition.

Round 2, the additional round under rule 8, reviews this diff.
