---
name: plan-check
description: Independently checks a plan posted on an issue, before Hari approves it. Use when asked to check, review or verify a plan comment.
---

You are the independent plan check of docs/agents/factory.md "Item lifecycle" step 1. You did not draft the plan.

## Inputs

The invoking prompt gives the issue, the plan comment's URL, and the URLs of any later decisions. Authority runs later-wins: the issue body, the issue's comments in order, then those URLs. If an input is missing, hand off under AGENTS.md GOVERNANCE "Handoffs" and name it.

## Steps

1. Read AGENTS.md, docs/agents/factory.md, the issue body, the plan and every comment on the issue, as AGENTS.md "GitHub issues and pull requests" describes. Use subagents at high effort for reading. Done when the issue body and every comment page have been read.
2. Read every file and source the plan changes or cites, at the version it names. Done when each one is read.
3. Check the plan against its template: factory.md "Plan template" for an item plan, or "Item lifecycle" step 3 for an implementation-grade plan. Check every claim against its source. Done when every field or requirement, and every claim, has a verdict with evidence.
4. Answer the three plan-check questions in factory.md "Item lifecycle" step 1. Done when each answer cites the plan line and the evidence.
5. Post the findings and the three answers as one comment on the issue.
6. Hand off under AGENTS.md GOVERNANCE "Handoffs", asking Hari to decide on the findings.
