---
name: repo-review
description: Cold review of authored changes against repository rules and the originating specification. Use under AGENTS.md "Before opening a pull request" rule 8, on code, Terraform, scenarios, workflows or docs.
---

Review the diff as a stranger to it. Read anything in the repository and any cited source, but judge only what the files say, never the author's intent. AGENTS.md "Before opening a pull request" rules 5 and 8 own review inputs, completion, scope, budgets and handoff; use their applicable main versions under step 2.

## Steps

1. Read the authored receipt for the review mode and identified revisions under AGENTS.md "Before opening a pull request" rule 8. For a comprehensive review, read the full diff and list every touched file. For a focused review, identify the changes, unresolved findings and affected context in that rule's scope; the full PR is context. If the review scope is empty, report `Findings: diff is empty` and stop.
2. For each file in the applicable review scope, find its rows in the rubric below and read every document those rows name. Where the diff changes one of those documents, or this skill, apply its version on `origin/main` (`git show origin/main:<path>`), so a change cannot weaken the rules it is reviewed against. If such a document has no `origin/main` version, apply the branch version and say so in the report. The step is done when every file in scope is matched to at least one row.
3. Check the applicable review scope against every rule in those documents. Read the source behind any factual claim (version-pinned where the repository pins one); judge from the source, never from memory.
4. Compare the changes in scope with the originating specification or selected current approved plan revision and recognized later decisions identified in the receipt. Read the sources; use docs/agents/factory.md "Item lifecycle" step 3 for authority recognition. For directly authorized governance work without an approved item plan, use the recorded task brief and issue without inventing plan approval. Identify missing, partial or incorrect requirements and scope creep with source evidence. Check changed and affected verifier predicates against the receipt's test map and docs/engineering/dotnet.md "Testing".
5. Check the applicable scope against itself and the rest of the repository: a change in one place that contradicts a rule, ADR, count or cross-reference elsewhere is a finding.
6. Report after completing the applicable scope. Apply every rule from step 2 to every file in that scope and report the specification comparison result. Use AGENTS.md "Before opening a pull request" rule 8 for unsafe focus or exhausted budget.

## Rubric

| The diff touches | Apply |
|---|---|
| anything | AGENTS.md: all of GOVERNANCE (scope and deferred items included), "Code Review Rules", "Before opening a pull request", "Skills", writing standards; CLAUDE.md; docs/agents/factory.md; docs/blueprint.md ADRs |
| `docs/engineering/` | each changed document itself |
| .NET code or tests | docs/engineering/dotnet.md |
| Terraform | docs/engineering/terraform.md; ADR-011 |
| Kafka client settings | docs/blueprint.md "Workloads (.NET 10, Confluent.Kafka 2.16.0 pinned)": every setting traceable to an inventory item |
| `scenarios/`, `results/` | AGENTS.md verdict integrity |
| `.github/workflows/` | AGENTS.md safety rules 8 and 9 |

## Report

Identify the review mode and reviewed/comparison revisions. State `Report: complete` only after finishing the applicable scope. Summarize specification coverage with source locations, including any missing, partial or incorrect requirement or scope creep.

Each finding's location is a line the diff adds, removes or changes; unchanged files serve only as evidence. One line per finding, most severe first:

`P0|P1|P2  path:line  rule (document and section)  what is wrong  evidence`

Severity follows AGENTS.md "Code Review Rules". Anything else that breaks a binding rule is P1. Apply AGENTS.md "Code Review Rules" for the P2 reporting bar. The diff does not show pull request labels, so raise a rule that depends on a label as if the label were absent; AGENTS.md "Before opening a pull request" rule 8 says how the author handles it.

Apply AGENTS.md "Before opening a pull request" rule 8 for the reporting mode of the prose-only P2-correction review.

After the findings, write exactly one result line: `Findings: none at P0 or P1` or `Findings: <n> at P0 or P1`. Below it, list problems you noticed in unchanged lines under `Pre-existing (not blocking):`.
