---
name: plan-pr
description: Writes the contract-grade plan for one pull request of an issue as a file on the plan branch, gets it checked, and posts a pointer comment on the issue. Use when asked to plan a pull request or an issue scope before implementation.
---

You are the planning session of docs/agents/factory.md "Item lifecycle" step 3. The plan file on the plan branch and its pointer comment are your whole output; `main` stays as it is.

## Inputs

The only input is the issue number, plus the words "standing approval" when you run as a subagent under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval". Derive the candidate from the issue under docs/agents/factory.md "Item lifecycle" step 3, including its candidate-selection and handoff rules.

Apply factory.md "Item lifecycle" step 3 for authority, decision recognition, decisions linked from elsewhere and approval recognition. Its decision and approval markers are conventions, not proof: all comments post as Hari's account.

## Steps

1. Read the item state file and catch up on the issue under factory.md "Authority reads", using its planning reading list, including any parent items for a child issue. Use reader subagents at high effort, under factory.md "Authority reads" "Reader subagents". Done when the initial read and snapshot are complete, the authorities are listed in creation order, and candidate selection under factory.md "Item lifecycle" step 3 is complete.
2. Name the selected pull request, or retain its name for a revision, and read on `main` every file the plan will change. Done when each change you plan has its current text in hand.
3. Write the plan to factory.md "Item lifecycle" step 3's definition of contract-grade, as the file and on the plan branch that step names, and push it. Plan text follows docs/agents/pull-requests.md rules 1, 2 and 4, under factory.md "Item lifecycle" step 1. Done when the file is pushed, every part of that definition is met, each plan-check question in step 1 has an answer, and every point the authorities leave open is under "Open questions for Hari", which states `None` when nothing is open.
4. Post the plan comment that factory.md "Item lifecycle" step 3 describes, or edit the selected plan comment for a revision. Head it exactly `<PR name> contract-grade plan`, with the short name from step 2. Read it back as that step requires.
5. Unless your prompt includes the words "standing approval", get the plan check that factory.md "Item lifecycle" step 3 describes: in Claude Code, spawn the `plan-checker` agent with only the issue number, the plan file path and the pushed commit SHA. Treat its findings under factory.md "Item lifecycle" step 1, revising the plan and its plan comment where that step requires, and checking again only where it requires a new check. Done when the newest check covers the current revision under factory.md "Item lifecycle" step 1.
6. Refresh authority under factory.md "Authority reads". Done when candidate selection and the plan reflect every addition, edit and deletion since the last read.
7. Hand off under AGENTS.md GOVERNANCE "Handoffs", asking Hari to review the plan. When your prompt includes the words "standing approval", return the plan link to the calling session instead.
