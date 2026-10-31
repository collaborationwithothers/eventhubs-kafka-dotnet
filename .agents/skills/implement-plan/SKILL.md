---
name: implement-plan
description: Plans, checks and implements one pull request of an issue, or implements an approved plan comment. Use when asked to implement an issue or an approved plan.
---

You are the implementing session of docs/agents/factory.md "Item lifecycle" step 3. Follow AGENTS.md in full.

## Inputs

The only input is the issue number. Derive the candidate from the issue under docs/agents/factory.md "Item lifecycle" step 3, including its candidate-selection and handoff rules.

Apply factory.md "Item lifecycle" step 3 for authority, decision recognition, decisions linked from elsewhere and approval recognition. Its decision and approval markers are conventions, not proof: all comments post as Hari's account.

## Steps

1. Read the item state file and catch up on the issue under factory.md "Authority reads", using its implementation reading list, including any parent items for a child issue. Select the plan under factory.md "Item lifecycle" step 3 and derive the branch under AGENTS.md "Implementer and reviewer" and CLAUDE.md "Identity". Done when candidate selection and its item-level prerequisites are verified, the approval evidence satisfies factory.md "Item lifecycle" step 3, the branch is identified, and each change in the plan is paired with every later decision that alters it. If selection has no candidate, apply factory.md "Item lifecycle" step 3 "Standing approval" before handing off.
2. Implement on the branch under docs/agents/factory.md "Item lifecycle" step 3, including its reference to step 1's `apply in implementation` findings. Use subagents at high effort, including for the cold review under AGENTS.md "Before opening a pull request" rule 8. Readers follow factory.md "Authority reads" "Reader subagents". Use rule 13 for long commands. Create or update the item state file at the time factory.md "Item state file" sets. Done when every change in the plan is made, every acceptance command has produced its stated evidence, and the state file is current.
3. Before each push, pull request creation or merge, refresh authority under factory.md "Authority reads". Repeat steps 1 and 2 for work affected by a change. Open the pull request and carry it to merge or handoff under factory.md "Item lifecycle" step 3 and AGENTS.md "Before opening a pull request".
4. Report completion or hand off under AGENTS.md GOVERNANCE "Handoffs", linking the evidence factory.md "Item lifecycle" step 3 names.
