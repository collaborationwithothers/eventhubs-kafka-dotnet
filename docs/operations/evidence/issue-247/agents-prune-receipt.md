# AGENTS.md pruning receipt

This receipt records a documentation-only change that shortens AGENTS.md without changing any rule. It moves rule basis, citations and decision history out of the text every session loads, and points to the pre-change file for them.

## Execution card

- Deliverable: a shorter AGENTS.md. Every operative rule, gate, cap, exception, exhaustion action and rule number, and every section heading except the restating "Workflow summary", stays in force with unchanged meaning. Only basis sentences, citations of past decisions, dated observations and text that restates docs/agents/factory.md are removed.
- Authorization: Hari's direct instruction in an interactive Claude Code session on 2026-10-31: "go ahead and propose the pruning we are wasting so many tokens. But the changes need to reviewed in a fresh context to make sure the quality and guard rails dont drop." This is directly authorized governance work without an approved item plan; no plan approval is claimed. Related issue: [retro on repeated review cycles and token use](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/247).
- Governance freeze: the freeze in [#85 decision 6318070052](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6318070052) item 3 lasts "until `results/A01/` holds its first file". On source commit `8cbde949d8fca424dda33accbd01c1a995495678`, `results/A01/2026-10-25.json` and `results/A01/2026-10-25.md` exist, added in commit `a6f6641848eb13fcc928b8b304f5efcef1d2ca5b`. Inferred: the freeze condition has ended. Hari confirms or rejects this at merge.
- Mandatory gates: `git diff --check`, `npm run check:docs`, one fresh-context cold review under AGENTS.md "Before opening a pull request" rule 8, CI, hosted Codex review, and Hari's merge because AGENTS.md is a protected path.
- Publication checkpoint: open a draft pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: rule 8's review-budget handoff to Hari.
- Predicate-to-test map: not applicable; no verifier work.

## Removed content

The diff removes these kinds of text:

1. The GOVERNANCE status paragraph's approval and amendment history. It is replaced by one sentence that keeps its operative content: AGENTS.md is protected and changes reach `main` only through Hari's merge.
2. "Basis:" sentences and links to the decisions that adopted a rule, in the #138 transition (including its "#85 record" link), Verdict integrity, Skills, Code Review Rules, Writing standards, and "Before opening a pull request" rules 6, 8, 10, 11, 12 and 13. Rule 12 loses "Basis: #85 decision 5; validated commits were lost when a Codex session resumed in a fresh workspace." Citations of factual claims stay where a claim stays: hard safety rule 6 (the 60-minute interval and the lookup interface) and hard safety rule 10 (the connector replies). The "governance choice, not a measurement" labels on rule 6's threshold and rule 8's review budget stay.
3. The section "Workflow summary" and its heading. It restated docs/agents/factory.md "Item lifecycle" in short, and no file cites it.
4. In "Implementer and reviewer": the label-application condition, the re-check on every push, and the undetected-cases sentence for the planned merge workflow. factory.md "Escalation" keeps the condition and re-check, and states "Neither the label nor the workflow exists yet." The words "for readers" on the branch-prefix bullet.
5. In "Skills": "An implementation prompt names `implement-plan` and the issue number.", which repeats the bullet above it.
6. The opening history sentence of "Before opening a pull request" about PR #6.
7. Rule 12's evidence sentence now pins the source commit instead of `0e9b78be2a3a175c6a27926479fe5cfe96ab1888`. The trigger description is kept and was rechecked on the source commit.
8. The "#138 transition" paragraph under GOVERNANCE, removed on Hari's instruction of 2026-10-31 after he confirmed the governance freeze has ended. Its pointer to the prerequisites and smoke-gate sequence in docs/agents/factory.md "Item lifecycle" is not needed, because factory.md is read by every session under "Authority reads".

The rationale and history stay reachable through the new pointer to AGENTS.md at the source commit, in the introduction.

## Verification

| Command or inspection | Observed result |
| --- | --- |
| `git diff --check` | Exit 0, no output. |
| `npm ci --ignore-scripts` then `npm run check:docs` | Completed with `Documentation scan completed. Warnings are advisory; external URLs were not checked.` No issues in AGENTS.md. 8 warnings, all in files this change does not touch: docs/agents/factory.md, docs/operations/evidence/issue-124/README.md, issue-140/README.md, issue-211/receipt.md. |
| Word count, scripted with Python `str.split()` | AGENTS.md: 4884 words on the source commit, 4375 after. |
| `grep -rn "Workflow summary"` over Markdown, shell, Python, JavaScript and JSON files, excluding node_modules | No matches outside this receipt. |
| Section headings and rule numbers, compared by script against the source commit | Rule numbers unchanged. One heading removed: "Workflow summary". |

Source commit: `8cbde949d8fca424dda33accbd01c1a995495678`.

## Cold review

| Round | Mode | Input revision | Comparison revision | Result |
| --- | --- | --- | --- | --- |
| 1 | Comprehensive, pass 1 of 2 | `70f91bfab1c769af5d9209b75ce292fd5c467fd7` | origin/main `8cbde949d8fca424dda33accbd01c1a995495678` | 5 P1, 2 P2. All fixed in `6264d30`. |
| 2 | Comprehensive, pass 2 of 2 | `6264d30` | origin/main `8cbde949d8fca424dda33accbd01c1a995495678` | 2 P1, 2 P2, all in this receipt or the AGENTS.md pointer sentence. All fixed in `70202b1`. |
| 3 | Focused, 1 of 2 | `70202b1` | `6264d30` | 1 P1: this table lacked per-round comparison revisions. Fixed in the next commit. |
| 4 | Focused, 2 of 2 | The commit after `70202b1` | `70202b1` | Reviews the round 3 fix, with the full PR diff as context. |

Complete reports and dispositions are in the pull request's self-review record.
