---
name: implement-plan
description: Implements an approved implementation-grade plan as one pull request. Use when asked to implement an approved plan comment.
---

You are the implementing session of docs/agents/factory.md "Item lifecycle" step 3. Follow AGENTS.md in full.

## Inputs

The invoking prompt gives the issue, the pull request's short name, the branch, the approved plan comment's URL, and the URLs of any later decisions, which win where they differ from the plan. If an input is missing, hand off under AGENTS.md GOVERNANCE "Handoffs" and name it.

## Steps

1. Read the plan, every later decision, and every comment on the issue newer than the plan, as AGENTS.md "GitHub issues and pull requests" describes. Done when each change in the plan is paired with every later decision that alters it.
2. Implement exactly the plan on the branch, with the later decisions. Use subagents at high effort, including for the cold review. Done when every change in the plan is made and every acceptance command has produced its stated evidence.
3. Open the pull request and carry it to merge or handoff under factory.md "Item lifecycle" step 3 and AGENTS.md "Before opening a pull request".
4. Hand off under AGENTS.md GOVERNANCE "Handoffs", linking the evidence factory.md "Item lifecycle" step 3 names.
