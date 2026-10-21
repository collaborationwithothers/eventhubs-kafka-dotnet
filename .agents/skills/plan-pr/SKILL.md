---
name: plan-pr
description: Writes the implementation-grade plan for one pull request of an issue and posts it as an issue comment. Use when asked to plan a pull request or an issue scope before implementation.
---

You are the planning session of docs/agents/factory.md "Item lifecycle" step 3. The posted plan is your whole output; the repository stays as it is.

## Inputs

The only input is the issue number, plus the words "standing approval" when you run as a subagent under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval". Derive the candidate from the issue under docs/agents/factory.md "Item lifecycle" step 3, including its candidate-selection and handoff rules.

Apply factory.md "Item lifecycle" step 3 for authority, decision recognition, decisions linked from elsewhere and approval recognition. Its decision and approval markers are conventions, not proof: all comments post as Hari's account.

## Steps

1. Read AGENTS.md, docs/agents/factory.md and the issue under factory.md "Authority reads", including any parent items for a child issue. Use subagents at high effort for reading. Done when the initial read and snapshot are complete, the authorities are listed in creation order, and candidate selection under factory.md "Item lifecycle" step 3 is complete.
2. Name the selected pull request, or retain its name for a revision, and read on `main` every file the plan will change. Done when each change you plan has its current text in hand.
3. Write the plan to factory.md "Item lifecycle" step 3's definition of implementation-grade. Done when every part of that definition is met, each plan-check question in step 1 has an answer, and every point the authorities leave open is under "Open questions for Hari", which states `None` when nothing is open.
4. Refresh authority under factory.md "Authority reads". Done when candidate selection and the plan reflect every addition, edit and deletion since the last read.
5. Post a new plan as one comment on the issue, or update the selected plan comment for a revision under factory.md "Item lifecycle" step 3. Head it exactly `<PR name> implementation-grade plan`, with the short name from step 2.
6. Hand off under AGENTS.md GOVERNANCE "Handoffs", asking Hari to review the plan. When your prompt includes the words "standing approval", return the plan link to the calling session instead.
