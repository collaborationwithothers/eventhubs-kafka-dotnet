# AGENTS.md pruning receipt

This receipt records a documentation-only change that shortens AGENTS.md without changing any rule. It moves rule basis, citations and decision history out of the text every session loads, and points to the pre-change file for them.

## Execution card

- Deliverable: a shorter AGENTS.md. Every operative rule, gate, cap, exception, exhaustion action and section heading and rule number stays in force with unchanged meaning. Only basis sentences, citations of past decisions, dated observations and text that restates docs/agents/factory.md are removed.
- Authorization: Hari's direct instruction in an interactive Claude Code session on 2026-10-31: "go ahead and propose the pruning we are wasting so many tokens. But the changes need to reviewed in a fresh context to make sure the quality and guard rails dont drop." This is directly authorized governance work without an approved item plan; no plan approval is claimed. Related issue: [retro on repeated review cycles and token use](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/247).
- Governance freeze: the freeze in [#85 decision 6318070052](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6318070052) item 3 lasts "until `results/A01/` holds its first file". On source commit `8cbde949d8fca424dda33accbd01c1a995495678`, `results/A01/2026-10-25.json` and `results/A01/2026-10-25.md` exist, added in commit `4f17a2050837e08f79392c8688a8e9bbe6920336`. Inferred: the freeze condition has ended. Hari confirms or rejects this at merge.
- Mandatory gates: `git diff --check`, `npm run check:docs`, one fresh-context cold review under AGENTS.md "Before opening a pull request" rule 8, CI, hosted Codex review, and Hari's merge because AGENTS.md is a protected path.
- Publication checkpoint: open a draft pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: rule 8's review-budget handoff to Hari.
- Predicate-to-test map: not applicable; no verifier work.

## Removed content

The diff removes these kinds of text:

1. The GOVERNANCE status paragraph's approval and amendment history. It is replaced by one sentence that keeps its operative content: AGENTS.md is protected and changes reach `main` only through Hari's merge.
2. "Basis:" sentences and links to the decisions that adopted a rule. These appear in the #138 transition, hard safety rules 6 and 10, Verdict integrity, Skills, and "Before opening a pull request" rules 5, 6, 8, 10, 11, 12 and 13, and in Code Review Rules and Writing standards.
3. The section "Workflow summary". It restated docs/agents/factory.md "Item lifecycle" in short, and no file cites it.
4. The undetected-cases sentence in "Implementer and reviewer", which described the planned merge workflow. That workflow does not exist yet (docs/agents/factory.md "Escalation": "Neither the label nor the workflow exists yet.").
5. The opening history sentence of "Before opening a pull request" about PR #6.
6. The pinned commit SHA in rule 12's evidence sentence. The trigger description it supported is kept.

The rationale and history stay reachable through the new pointer to AGENTS.md at the source commit, in the introduction.

## Verification

| Command or inspection | Observed result |
| --- | --- |
| `git diff --check` | Exit 0, no output. |
| `npm ci --ignore-scripts` then `npm run check:docs` | Completed with `Documentation scan completed. Warnings are advisory; external URLs were not checked.` No issues in AGENTS.md. 8 warnings, all in files this change does not touch: docs/agents/factory.md, docs/operations/evidence/issue-124/README.md, issue-140/README.md, issue-211/receipt.md. |
| Word count, scripted with Python `str.split()` | AGENTS.md: 4884 words on the source commit, 4380 after. |
| `grep -rn "Workflow summary"` over Markdown, shell, Python, JavaScript and JSON files, excluding node_modules | No matches. |
| Section headings and rule numbers | Unchanged. Checked by the cold review. |

Source commit: `8cbde949d8fca424dda33accbd01c1a995495678`.

## Cold review

Results are recorded in the pull request's self-review record.
