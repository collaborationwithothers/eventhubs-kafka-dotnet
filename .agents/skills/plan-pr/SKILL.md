---
name: plan-pr
description: Writes the implementation-grade plan for one pull request of an issue and posts it as an issue comment. Use when asked to plan a pull request or an issue scope before implementation.
---

You are the planning session of docs/agents/factory.md "Item lifecycle" step 3. The posted plan is your whole output; the repository stays as it is.

## Inputs

The only input is the issue number. Derive the candidate from the issue under docs/agents/factory.md "Item lifecycle" step 3, including its candidate-selection and handoff rules.

Apply factory.md "Item lifecycle" step 3 for authority, decision recognition, decisions linked from elsewhere and approval recognition. Its decision and approval markers are conventions, not proof: all comments post as Hari's account.

## Steps

1. Read AGENTS.md, docs/agents/factory.md, the issue body and every comment on the issue, as AGENTS.md "GitHub issues and pull requests" describes. Use subagents at high effort for reading. Done when the issue body, every comment page and the decisions linked from issue comments have been read, the authorities are listed in creation order, and candidate selection under factory.md "Item lifecycle" step 3 is complete.
2. Name the selected pull request and read on `main` every file the plan will change. Done when each change you plan has its current text in hand.
3. Write the plan to factory.md "Item lifecycle" step 3's definition of implementation-grade. Done when every part of that definition is met, each plan-check question in step 1 has an answer, and every point the authorities leave open is under "Open questions for Hari".
4. Read the issue body and list its comments again. Done when their text is unchanged since step 1, or the plan has been revised for every addition, edit or deletion.
5. Post the plan as one comment on the issue, headed exactly `<PR name> implementation-grade plan`, with the short name chosen in step 2.
6. Hand off under AGENTS.md GOVERNANCE "Handoffs", asking Hari to review the plan.
