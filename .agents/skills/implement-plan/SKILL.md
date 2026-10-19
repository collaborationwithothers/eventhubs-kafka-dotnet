---
name: implement-plan
description: Implements an approved implementation-grade plan as one pull request. Use when asked to implement an approved plan comment.
---

You are the implementing session of docs/agents/factory.md "Item lifecycle" step 3. Follow AGENTS.md in full.

## Inputs

The only input is the issue number. Derive the candidate from the issue under docs/agents/factory.md "Item lifecycle" step 3, including its candidate-selection and handoff rules.

Apply factory.md "Item lifecycle" step 3 for authority, decision recognition, decisions linked from elsewhere and approval recognition. Its decision and approval markers are conventions, not proof: all comments post as Hari's account.

## Steps

1. Read the issue body, every comment and the decisions linked from issue comments, as AGENTS.md "GitHub issues and pull requests" describes. Select the plan under factory.md "Item lifecycle" step 3 and derive the branch under AGENTS.md "Implementer and reviewer" and CLAUDE.md "Identity". Done when candidate selection and its item-level prerequisites are verified, the approval evidence satisfies factory.md "Item lifecycle" step 3, the branch is identified, and each change in the plan is paired with every later decision that alters it.
2. Implement exactly the plan on the branch, with the later decisions. Use subagents at high effort, including for the cold review. Done when every change in the plan is made and every acceptance command has produced its stated evidence.
3. Before each push, pull request creation or merge, re-read the issue body, the plan, the later decisions and all issue comments. Repeat steps 1 and 2 if any authority was added, edited or deleted. Open the pull request and carry it to merge or handoff under factory.md "Item lifecycle" step 3 and AGENTS.md "Before opening a pull request".
4. Report completion or hand off under AGENTS.md GOVERNANCE "Handoffs", linking the evidence factory.md "Item lifecycle" step 3 names.
