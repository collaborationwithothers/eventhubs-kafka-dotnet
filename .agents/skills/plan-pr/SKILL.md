---
name: plan-pr
description: Writes the implementation-grade plan for one pull request of an issue and posts it as an issue comment. Use when asked to plan a pull request or an issue scope before implementation.
---

You are the planning session of docs/agents/factory.md "Item lifecycle" step 3. The posted plan is your whole output; the repository stays as it is.

## Inputs

The invoking prompt gives the issue, the pull request's short name (for example `PR 4`), and the URLs of any later decisions. Authority runs later-wins: the issue body, the issue's comments in order, then those URLs. If an input is missing, hand off under AGENTS.md GOVERNANCE "Handoffs" and name it.

## Steps

1. Read AGENTS.md, docs/agents/factory.md, the issue body and every comment on the issue, as AGENTS.md "GitHub issues and pull requests" describes. Use subagents at high effort for reading. Done when the issue body and every comment page have been read and the authorities are listed in later-wins order.
2. Read on `main` every file the plan will change. Done when each change you plan has its current text in hand.
3. Write the plan to factory.md "Item lifecycle" step 3's definition of implementation-grade. Done when every part of that definition is met, each plan-check question in step 1 has an answer, and every point the authorities leave open is under "Open questions for Hari".
4. Read the issue body and list its comments again. Done when their text is unchanged since step 1, or the plan has been revised for every addition, edit or deletion.
5. Post the plan as one comment on the issue, headed exactly `<PR name> implementation-grade plan`, with the short name from the inputs.
6. Hand off under AGENTS.md GOVERNANCE "Handoffs", asking Hari to review the plan.
