---
name: repo-review
description: Cold review of a diff against this repository's own rules (AGENTS.md, the blueprint's ADRs, docs/engineering/). Use before every push, on code, Terraform, scenarios, workflows or docs.
---

Review the diff as a stranger to it. Read anything in the repository and any cited source, but judge only what the files say, never the author's intent.

## Steps

1. Read the diff in full. List every file it touches.
2. For each file, find its rows in the rubric below and read every document those rows name. The step is done when every touched file is matched to at least one row.
3. Check the diff against every rule in those documents. Read the source behind any factual claim (version-pinned where the repository pins one); judge from the source, never from memory.
4. Check the diff against itself and the rest of the repository: a change in one place that contradicts a rule, ADR, count or cross-reference elsewhere is a finding.
5. Report. The review is done when every rule from step 2 has been applied to every touched file.

## Rubric

| The diff touches | Apply |
|---|---|
| anything | AGENTS.md: all of GOVERNANCE (scope and deferred items included), "Code Review Rules", "Before opening a pull request", "Skills", writing standards; docs/agents/factory.md; docs/blueprint.md ADRs |
| .NET code or tests | docs/engineering/dotnet.md |
| Terraform | docs/engineering/terraform.md; ADR-011 |
| Kafka client settings | docs/blueprint.md: every setting traced to an inventory item or an ADR; a setting that contradicts the blueprint needs a linked inventory item |
| `scenarios/`, `results/` | AGENTS.md verdict integrity |
| `.github/workflows/` | AGENTS.md safety rules 8 and 9 |

## Report

One line per finding, most severe first:

`P0|P1|P2  path:line  rule (document and section)  what is wrong  evidence`

Severity follows AGENTS.md "Code Review Rules"; anything else that breaks a binding rule is P1, and wording or clarity is P2. End with exactly one of: `Findings: none at P0 or P1` or `Findings: <n> at P0 or P1`.
