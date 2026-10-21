---
name: plan-check
description: Independently checks a plan posted on an issue, before it is approved. Use when asked to check, review or verify a plan comment.
---

You are the independent plan check of docs/agents/factory.md "Item lifecycle" step 1. You did not draft the plan.

## Inputs

The only input is the issue number. Derive the candidate from the issue under docs/agents/factory.md "Item lifecycle" step 3, including its candidate-selection and handoff rules.

Apply factory.md "Item lifecycle" step 3 for authority, decision recognition, decisions linked from elsewhere and approval recognition. Its decision and approval markers are conventions, not proof: all comments post as Hari's account.

## Steps

1. Read AGENTS.md, docs/agents/factory.md and the issue under factory.md "Authority reads", including any parent items for a child issue. Use subagents at high effort for reading. Done when the initial read and snapshot are complete, and candidate selection under factory.md "Item lifecycle" step 3 is complete.
2. Read every file and source the plan changes or cites, at the version it names. Done when each one is read.
3. Check the selected plan against factory.md "Plan template" for an item plan, or "Item lifecycle" step 3 for an implementation-grade plan. Check every claim against its source. Done when every field or requirement, and every claim, has a verdict with evidence.
4. Answer the three plan-check questions in factory.md "Item lifecycle" step 1. Done when each answer cites the plan line and the evidence.
5. Refresh authority under factory.md "Authority reads". Repeat steps 1 to 4 for work affected by a change. Done when the check reflects the current candidate and authority.
6. Post the findings and the three answers as one comment on the issue, using the check-comment format in factory.md "Item lifecycle" step 1. Include the `Blocking findings:` line that format requires.
7. Hand off under AGENTS.md GOVERNANCE "Handoffs", asking Hari to approve or reject the checked plan in light of the findings and the three answers. When your prompt includes the words "standing approval", return the comment link to the calling session instead.
